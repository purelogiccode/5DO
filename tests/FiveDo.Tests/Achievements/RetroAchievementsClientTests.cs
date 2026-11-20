#pragma warning disable MA0004
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FiveDo.Achievements;
using FiveDo.Achievements.Models;

namespace FiveDo.Tests.Achievements;

public sealed class RetroAchievementsClientTests
{
    private int _awardRequests;
    private int _online;
    private int _lastAwardHardcore;
    private int _leaderboardRequests;

    [Fact]
    public void InvalidHostFallsBackToTheDefaultInsteadOfThrowing()
    {
        using var client = new RetroAchievementsClient("ftp://example.invalid");

        Assert.Equal(RetroAchievementsClient.DefaultHost, client.Host);
    }

    [Fact]
    public async Task LoginParsesTokenAndScores()
    {
        var handler = new FakeHandler(_ => Json("""
            {"Success":true,"User":"Player","Token":"abc123","Score":120,"SoftcoreScore":80,"Messages":2}
            """));
        using var client = new RetroAchievementsClient(httpClient: new HttpClient(handler));

        RaLoginResult result = await client.LoginAsync("player", "secret", isToken: false);

        Assert.True(result.Success);
        Assert.Equal("Player", result.DisplayName);
        Assert.Equal("abc123", result.Token);
        Assert.Equal(120u, result.Score);
        Assert.Equal(80u, result.SoftcoreScore);
        Assert.Equal(2u, result.UnreadMessages);
        Assert.Equal("login2", handler.LastForm["r"]);
        Assert.Equal("secret", handler.LastForm["p"]);
    }

    [Fact]
    public async Task ResolveHashParsesGameId()
    {
        var handler = new FakeHandler(_ => Json("""{"Success":true,"GameID":4242}"""));
        using var client = new RetroAchievementsClient(httpClient: new HttpClient(handler));

        RaGameIdResult result = await client.ResolveHashAsync("0123456789abcdef0123456789abcdef");

        Assert.True(result.Success);
        Assert.Equal(4242u, result.GameId);
        Assert.Equal("gameid", handler.LastForm["r"]);
        Assert.Equal("0123456789abcdef0123456789abcdef", handler.LastForm["m"]);
    }

    [Fact]
    public async Task FetchGameParsesAchievementsAndRichPresence()
    {
        var handler = new FakeHandler(_ => Json("""
            {
              "Success": true,
              "PatchData": {
                "ID": 77,
                "Title": "Test Game",
                "ConsoleID": 43,
                "ImageIcon": "/Images/00001.png",
                "RichPresencePatch": "Display:\nTest",
                "Achievements": [
                  {
                    "ID": 10,
                    "Title": "[m] Missable",
                    "Description": "Do it",
                    "Flags": 3,
                    "Points": 25,
                    "MemAddr": "0xH0000=1",
                    "Author": "Author",
                    "BadgeName": "0010",
                    "Created": 1,
                    "Modified": 2,
                    "Type": "progression",
                    "Rarity": 12.5,
                    "RarityHardcore": 3.5
                  }
                ],
                "Leaderboards": [
                  {
                    "ID": 20,
                    "Title": "Fastest",
                    "Description": "Go fast",
                    "Mem": "STA:0xH0000=1::CAN:0xH0001=1::SUB:0xH0002=1::VAL:0xH0003",
                    "Format": "TIME",
                    "LowerIsBetter": true,
                    "Hidden": false
                  }
                ]
              }
            }
            """));
        using var client = new RetroAchievementsClient(httpClient: new HttpClient(handler));

        RaGameData? game = await client.FetchGameAsync(77, "player", "token");

        Assert.NotNull(game);
        Assert.Equal(77u, game.GameId);
        Assert.Equal("Test Game", game.Title);
        Assert.Equal("patch", handler.LastForm["r"]);
        Assert.Equal("77", handler.LastForm["g"]);
        Assert.Equal("/dorequest.php", handler.LastPath);
        Assert.Equal(43u, game.ConsoleId);
        Assert.Equal("https://media.retroachievements.org/Images/00001.png", game.ImageUrl);
        Assert.Single(game.Achievements);
        Assert.Equal("Missable", game.Achievements[0].Title);
        Assert.True(game.Achievements[0].IsOfficial);
        Assert.Equal("https://media.retroachievements.org/Badge/0010.png", game.Achievements[0].BadgeUrl);
        Assert.Equal("https://media.retroachievements.org/Badge/0010_lock.png", game.Achievements[0].BadgeLockedUrl);
        Assert.Single(game.Leaderboards);
        Assert.True(game.Leaderboards[0].LowerIsBetter);
        Assert.Equal("TIME", game.Leaderboards[0].Format);
    }

    [Fact]
    public async Task SessionAndAwardParseResponses()
    {
        var handler = new FakeHandler(request =>
        {
            string action = handlerAction(request);
            if (string.Equals(action, "startsession", StringComparison.Ordinal))
            {
                return Json("""
                    {"Success":true,"Unlocks":[{"ID":1,"When":1700000000}],"HardcoreUnlocks":[{"ID":2,"When":1700000001}],"ServerNow":1700000002}
                    """);
            }

            return Json("""
                {"Success":true,"Score":150,"SoftcoreScore":100,"AchievementID":10,"AchievementsRemaining":4}
                """);

            string handlerAction(HttpRequestMessage message)
            {
                string body = message.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                int index = body.IndexOf("r=", StringComparison.Ordinal);
                return index < 0 ? string.Empty : body[(index + 2)..].Split('&')[0];
            }
        });
        using var client = new RetroAchievementsClient(httpClient: new HttpClient(handler));

        RaSessionResult session = await client.StartSessionAsync(77, "hash", "player", "token", hardcore: true);
        Assert.True(session.Success);
        Assert.Single(session.Unlocks);
        Assert.Equal(1u, session.Unlocks[0].AchievementId);
        Assert.Single(session.HardcoreUnlocks);
        Assert.Equal(1700000002, session.ServerNow);

        RaAwardResult award = await client.AwardAchievementAsync(10, "player", "token", hardcore: true, "hash");
        Assert.True(award.Success);
        Assert.Equal(150u, award.NewScore);
        Assert.Equal(100u, award.NewSoftcoreScore);
        Assert.Equal(10u, award.AwardedAchievementId);
        Assert.Equal(4u, award.AchievementsRemaining);
    }

    [Fact]
    public async Task ServiceLoadsAndUnlocksAchievement()
    {
        var handler = new FakeHandler(request =>
        {
            string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (body.Contains("r=patch", StringComparison.Ordinal))
            {
                return Json("""
                    {
                      "Success": true,
                      "PatchData": {
                        "ID": 77,
                        "Title": "Test Game",
                        "ConsoleID": 43,
                        "Achievements": [
                          {"ID":101000001,"Title":"Warning: Unknown Emulator","Description":"","Flags":3,"Points":0,"MemAddr":"1=1.300.","Author":"","BadgeName":"","Created":1,"Modified":2,"Type":"","Rarity":0.0,"RarityHardcore":0.0},
                          {"ID":10,"Title":"Unlock","Description":"Do it","Flags":3,"Points":10,"MemAddr":"0xH0000=1","Author":"A","BadgeName":"0010","Created":1,"Modified":2,"Type":"","Rarity":10.0,"RarityHardcore":5.0}
                        ],
                        "Leaderboards": []
                      }
                    }
                    """);
            }

            if (body.Contains("r=gameid", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"GameID":77}""");
            }

            if (body.Contains("r=startsession", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"Unlocks":[{"ID":101000001,"When":1700000000}],"HardcoreUnlocks":[],"ServerNow":1700000000}""");
            }

            if (body.Contains("r=awardachievement", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _awardRequests);
                return Json("""{"Success":true,"Score":10,"SoftcoreScore":10,"AchievementID":10,"AchievementsRemaining":0}""");
            }

            return Json("""{"Success":true}""");
        });

        var settings = new RetroAchievementsSettings
        {
            Enabled = true,
            Username = "player",
            ApiToken = "token",
        };

        var memory = new OneByteMemory(0);
        using var service = new RetroAchievementsService(
            memory,
            settings,
            new HttpClient(handler));

        var unlocked = new TaskCompletionSource<AchievementNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Notification += (_, args) =>
        {
            if (args.Notification.Kind == AchievementNotificationKind.AchievementUnlocked)
            {
                unlocked.TrySetResult(args.Notification);
            }
        };

        service.BeginGame("0123456789abcdef0123456789abcdef", "Test Game");

        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (service.State.GameLoaded is false && DateTime.UtcNow < deadline)
        {
            service.Tick();
            await Task.Delay(20);
        }

        Assert.True(service.State.GameLoaded, service.State.Error);
        Assert.Equal(1, service.State.TotalAchievements);
        Assert.Equal(0, service.State.UnlockedAchievements);
        Assert.Equal(10u, service.State.TotalPoints);
        service.Tick();
        memory.Value = 1;
        service.Tick();

        AchievementNotification notification = await unlocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("Unlock", notification.Title);
        Assert.Equal(10u, notification.Points);

        deadline = DateTime.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref _awardRequests) == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, Volatile.Read(ref _awardRequests));
        Assert.Single(service.State.Achievements);
        Assert.True(service.State.Achievements[0].Unlocked);
    }

    [Fact]
    public async Task ServiceQueuesUnlockAndSyncsWhenServerReturns()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "5do-ra-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string queuePath = Path.Combine(directory, "pending.json");
        try
        {
            var handler = new FakeHandler(request =>
            {
                string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                if (body.Contains("r=patch", StringComparison.Ordinal))
                {
                    return Json("""
                        {
                          "Success": true,
                          "PatchData": {
                            "ID": 77,
                            "Title": "Test Game",
                            "ConsoleID": 43,
                            "Achievements": [
                              {"ID":10,"Title":"Unlock","Description":"Do it","Flags":3,"Points":10,"MemAddr":"0xH0000=1","Author":"A","BadgeName":"0010","Created":1,"Modified":2,"Type":"","Rarity":10.0,"RarityHardcore":5.0}
                            ],
                            "Leaderboards": []
                          }
                        }
                        """);
                }

                if (body.Contains("r=gameid", StringComparison.Ordinal))
                {
                    return Json("""{"Success":true,"GameID":77}""");
                }

                if (body.Contains("r=startsession", StringComparison.Ordinal))
                {
                    return Json("""{"Success":true,"Unlocks":[],"HardcoreUnlocks":[],"ServerNow":1700000000}""");
                }

                if (body.Contains("r=awardachievement", StringComparison.Ordinal))
                {
                    if (Volatile.Read(ref _online) == 0)
                    {
                        return Json("""{"Success":false,"Error":"Rejected"}""");
                    }

                    Interlocked.Increment(ref _awardRequests);
                    return Json("""{"Success":true,"Score":10,"SoftcoreScore":10,"AchievementID":10,"AchievementsRemaining":0}""");
                }

                return Json("""{"Success":true}""");
            });

            var settings = new RetroAchievementsSettings
            {
                Enabled = true,
                Username = "player",
                ApiToken = "token",
            };

            var memory = new OneByteMemory(0);
            using var service = new RetroAchievementsService(
                memory,
                settings,
                new HttpClient(handler),
                queuePath);

            var unlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Notification += (_, args) =>
            {
                if (args.Notification.Kind == AchievementNotificationKind.AchievementUnlocked)
                {
                    unlocked.TrySetResult();
                }
            };

            service.BeginGame("0123456789abcdef0123456789abcdef", "Test Game");
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!service.State.GameLoaded && DateTime.UtcNow < deadline)
            {
                service.Tick();
                await Task.Delay(20);
            }

            Assert.True(service.State.GameLoaded, service.State.Error);
            service.Tick();
            memory.Value = 1;
            service.Tick();
            await unlocked.Task.WaitAsync(TimeSpan.FromSeconds(10));

            deadline = DateTime.UtcNow.AddSeconds(10);
            while (service.State.PendingUnlocks == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.Equal(1, service.State.PendingUnlocks);
            Assert.Equal(0, Volatile.Read(ref _awardRequests));

            Volatile.Write(ref _online, 1);
            deadline = DateTime.UtcNow.AddSeconds(10);
            while (service.State.PendingUnlocks > 0 && DateTime.UtcNow < deadline)
            {
                await service.FlushPendingUnlocksOnceAsync(CancellationToken.None);
                await Task.Delay(20);
            }

            Assert.Equal(0, service.State.PendingUnlocks);
            Assert.Equal(1, Volatile.Read(ref _awardRequests));
            Assert.Equal(10u, service.State.SoftcoreScore);
            Assert.Equal(0, new PendingUnlockQueue(queuePath).Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ServiceProgressSurvivesExportAndImport()
    {
        var handler = new FakeHandler(request =>
        {
            string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (body.Contains("r=patch", StringComparison.Ordinal))
            {
                return Json("""
                    {
                      "Success": true,
                      "PatchData": {
                        "ID": 77,
                        "Title": "Test Game",
                        "ConsoleID": 43,
                        "Achievements": [
                          {"ID":10,"Title":"Four Hits","Description":"","Flags":3,"Points":10,"MemAddr":"0xH0000=1.4.","Author":"","BadgeName":"","Created":1,"Modified":2,"Type":"","Rarity":10.0,"RarityHardcore":5.0}
                        ],
                        "Leaderboards": []
                      }
                    }
                    """);
            }

            if (body.Contains("r=gameid", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"GameID":77}""");
            }

            if (body.Contains("r=startsession", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"Unlocks":[],"HardcoreUnlocks":[],"ServerNow":1700000000}""");
            }

            if (body.Contains("r=awardachievement", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _awardRequests);
                return Json("""{"Success":true,"Score":10,"SoftcoreScore":10,"AchievementID":10,"AchievementsRemaining":0}""");
            }

            return Json("""{"Success":true}""");
        });

        var settings = new RetroAchievementsSettings
        {
            Enabled = true,
            Username = "player",
            ApiToken = "token",
        };

        var memory = new ByteArrayMemory(4);
        using var service = new RetroAchievementsService(memory, settings, new HttpClient(handler));
        var unlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Notification += (_, args) =>
        {
            if (args.Notification.Kind == AchievementNotificationKind.AchievementUnlocked)
            {
                unlocked.TrySetResult();
            }
        };

        service.BeginGame("0123456789abcdef0123456789abcdef", "Test Game");
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!service.State.GameLoaded && DateTime.UtcNow < deadline)
        {
            service.Tick();
            await Task.Delay(20);
        }

        Assert.True(service.State.GameLoaded, service.State.Error);
        memory.Set(0, 1);
        service.Tick();
        service.Tick();
        service.Tick();
        Assert.False(service.State.Achievements[0].Unlocked);
        byte[] progress = service.ExportProgress();
        Assert.NotEmpty(progress);

        service.ResetRuntime();
        service.Tick();
        Assert.True(service.ImportProgress(progress));
        service.Tick();

        await unlocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(service.State.Achievements[0].Unlocked);
    }

    [Fact]
    public async Task HardcoreSessionKeepsCasualOnlyUnlockActive()
    {
        var handler = new FakeHandler(request =>
        {
            string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (body.Contains("r=patch", StringComparison.Ordinal))
            {
                return Json("""
                    {
                      "Success": true,
                      "PatchData": {
                        "ID": 77,
                        "Title": "Test Game",
                        "ConsoleID": 43,
                        "Achievements": [
                          {"ID":10,"Title":"Casual Only","Description":"","Flags":3,"Points":10,"MemAddr":"0xH0000=1","Author":"","BadgeName":"","Created":1,"Modified":2,"Type":"","Rarity":10.0,"RarityHardcore":5.0},
                          {"ID":11,"Title":"Hardcore Done","Description":"","Flags":3,"Points":20,"MemAddr":"0xH0001=1","Author":"","BadgeName":"","Created":1,"Modified":2,"Type":"","Rarity":10.0,"RarityHardcore":5.0}
                        ],
                        "Leaderboards": []
                      }
                    }
                    """);
            }

            if (body.Contains("r=gameid", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"GameID":77}""");
            }

            if (body.Contains("r=startsession", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"Unlocks":[{"ID":10}],"HardcoreUnlocks":[{"ID":11}],"ServerNow":1700000000}""");
            }

            if (body.Contains("r=awardachievement", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _awardRequests);
                Volatile.Write(ref _lastAwardHardcore, body.Contains("h=1", StringComparison.Ordinal) ? 1 : 0);
                return Json("""{"Success":true,"Score":20,"SoftcoreScore":20,"AchievementID":10,"AchievementsRemaining":1}""");
            }

            return Json("""{"Success":true}""");
        });

        var settings = new RetroAchievementsSettings
        {
            Enabled = true,
            Username = "player",
            ApiToken = "token",
            Hardcore = true,
        };

        var memory = new ByteArrayMemory(4);
        using var service = new RetroAchievementsService(memory, settings, new HttpClient(handler));

        var unlocked = new TaskCompletionSource<AchievementNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Notification += (_, args) =>
        {
            if (args.Notification.Kind == AchievementNotificationKind.AchievementUnlocked)
            {
                unlocked.TrySetResult(args.Notification);
            }
        };

        service.BeginGame("0123456789abcdef0123456789abcdef", "Test Game");
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!service.State.GameLoaded && DateTime.UtcNow < deadline)
        {
            service.Tick();
            await Task.Delay(20);
        }

        Assert.True(service.State.GameLoaded, service.State.Error);
        Assert.Equal(2, service.State.TotalAchievements);
        Assert.Equal(1, service.State.UnlockedAchievements);
        Assert.Equal(20u, service.State.UnlockedPoints);
        Assert.False(service.State.Achievements.Single(achievement => achievement.Id == 10).Unlocked);
        Assert.True(service.State.Achievements.Single(achievement => achievement.Id == 11).Unlocked);

        service.Tick();
        memory.Set(0, 1);
        service.Tick();

        AchievementNotification notification = await unlocked.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(10u, notification.AchievementId);

        deadline = DateTime.UtcNow.AddSeconds(10);
        while (Volatile.Read(ref _awardRequests) == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, Volatile.Read(ref _awardRequests));
        Assert.Equal(1, Volatile.Read(ref _lastAwardHardcore));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task LeaderboardEntriesAreOnlySubmittedInHardcore(bool hardcore, int expectedSubmissions)
    {
        var handler = new FakeHandler(request =>
        {
            string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (body.Contains("r=patch", StringComparison.Ordinal))
            {
                return Json("""
                    {
                      "Success": true,
                      "PatchData": {
                        "ID": 77,
                        "Title": "Test Game",
                        "ConsoleID": 43,
                        "Achievements": [],
                        "Leaderboards": [
                          {
                            "ID": 20,
                            "Title": "Fastest",
                            "Description": "Go fast",
                            "Mem": "STA:0xH0000=1::CAN:0xH0001=1::SUB:0xH0002=1::VAL:0xH0003",
                            "Format": "TIME",
                            "LowerIsBetter": true,
                            "Hidden": false
                          }
                        ]
                      }
                    }
                    """);
            }

            if (body.Contains("r=gameid", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"GameID":77}""");
            }

            if (body.Contains("r=startsession", StringComparison.Ordinal))
            {
                return Json("""{"Success":true,"Unlocks":[],"HardcoreUnlocks":[],"ServerNow":1700000000}""");
            }

            if (body.Contains("r=submitlbentry", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _leaderboardRequests);
                return Json("""{"Success":true,"Response":{"Score":1,"BestScore":1,"RankInfo":{"Rank":1,"NumEntries":1}}}""");
            }

            return Json("""{"Success":true}""");
        });

        var settings = new RetroAchievementsSettings
        {
            Enabled = true,
            Username = "player",
            ApiToken = "token",
            Hardcore = hardcore,
        };

        var memory = new ByteArrayMemory(4);
        using var service = new RetroAchievementsService(memory, settings, new HttpClient(handler));

        service.BeginGame("0123456789abcdef0123456789abcdef", "Test Game");
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!service.State.GameLoaded && DateTime.UtcNow < deadline)
        {
            service.Tick();
            await Task.Delay(20);
        }

        Assert.True(service.State.GameLoaded, service.State.Error);
        service.Tick();
        memory.Set(0, 1);
        service.Tick();
        memory.Set(2, 1);
        service.Tick();

        deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref _leaderboardRequests) < expectedSubmissions && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        if (expectedSubmissions == 0)
        {
            await Task.Delay(250);
        }

        Assert.Equal(expectedSubmissions, Volatile.Read(ref _leaderboardRequests));
    }

    [Fact]
    public async Task PendingUnlockForAnotherGameDoesNotBlockCurrentGame()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "5do-ra-queue-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string queuePath = Path.Combine(directory, "pending.json");
        try
        {
            const string activeHash = "0123456789abcdef0123456789abcdef";
            const string otherHash = "fedcba9876543210fedcba9876543210";
            var seeded = new PendingUnlockQueue(queuePath);
            Assert.True(seeded.Add(new PendingUnlock(
                11,
                88,
                otherHash,
                "player",
                false,
                DateTimeOffset.UtcNow,
                "Other Game",
                10)));
            Assert.True(seeded.Add(new PendingUnlock(
                10,
                77,
                activeHash,
                "player",
                false,
                DateTimeOffset.UtcNow,
                "Current Game",
                10)));

            var awarded = new ConcurrentQueue<uint>();
            var handler = new FakeHandler(request =>
            {
                string body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                if (body.Contains("r=patch", StringComparison.Ordinal))
                {
                    return Json("""
                        {
                          "Success": true,
                          "PatchData": {
                            "ID": 77,
                            "Title": "Current Game",
                            "ConsoleID": 43,
                            "Achievements": [],
                            "Leaderboards": []
                          }
                        }
                        """);
                }

                if (body.Contains("r=gameid", StringComparison.Ordinal))
                {
                    return Json("""{"Success":true,"GameID":77}""");
                }

                if (body.Contains("r=startsession", StringComparison.Ordinal))
                {
                    return Json("""{"Success":true,"Unlocks":[],"HardcoreUnlocks":[],"ServerNow":1700000000}""");
                }

                if (body.Contains("r=awardachievement", StringComparison.Ordinal))
                {
                    int start = body.IndexOf("a=", StringComparison.Ordinal);
                    string idText = start < 0 ? string.Empty : body[(start + 2)..].Split('&')[0];
                    if (uint.TryParse(idText, out uint id))
                    {
                        awarded.Enqueue(id);
                    }

                    return Json("""{"Success":true,"Score":10,"SoftcoreScore":10,"AchievementID":10,"AchievementsRemaining":0}""");
                }

                return Json("""{"Success":true}""");
            });

            var settings = new RetroAchievementsSettings
            {
                Enabled = true,
                Username = "player",
                ApiToken = "token",
            };

            using var service = new RetroAchievementsService(
                new ByteArrayMemory(4),
                settings,
                new HttpClient(handler),
                queuePath);

            service.BeginGame(activeHash, "Current Game");
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!service.State.GameLoaded && DateTime.UtcNow < deadline)
            {
                service.Tick();
                await Task.Delay(20);
            }

            Assert.True(service.State.GameLoaded, service.State.Error);
            deadline = DateTime.UtcNow.AddSeconds(10);
            while (awarded.IsEmpty && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.Equal([10u], awarded.ToArray());
            deadline = DateTime.UtcNow.AddSeconds(10);
            while (service.State.PendingUnlocks > 1 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20);
            }

            Assert.Equal(1, service.State.PendingUnlocks);
            Assert.Equal(1, new PendingUnlockQueue(queuePath).Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public Dictionary<string, string> LastForm { get; } = new(StringComparer.Ordinal);

        public string LastPath { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (request.Content is not null)
            {
                string body = await request.Content.ReadAsStringAsync(cancellationToken);
                LastForm.Clear();
                foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    int equals = pair.IndexOf('=', StringComparison.Ordinal);
                    string key = equals < 0 ? pair : pair[..equals];
                    string value = equals < 0 ? string.Empty : Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
                    LastForm[Uri.UnescapeDataString(key)] = value;
                }
            }

            return responder(request);
        }
    }

    private sealed class ByteArrayMemory(int length) : IAchievementMemory
    {
        private readonly byte[] _bytes = new byte[length];

        public void Set(int index, byte value) => _bytes[index] = value;

        public uint Peek(uint address, uint numBytes)
        {
            if (numBytes is 0 or > 4)
            {
                return 0;
            }

            uint value = 0;
            for (uint index = 0; index < numBytes; index++)
            {
                ulong byteIndex = (ulong)address + index;
                if (byteIndex >= (ulong)_bytes.Length)
                {
                    return 0;
                }

                value |= (uint)_bytes[(int)byteIndex] << (int)(index * 8);
            }

            return value;
        }
    }

    private sealed class OneByteMemory(byte value) : IAchievementMemory
    {
        public byte Value { get; set; } = value;

        public uint Peek(uint address, uint numBytes)
        {
            if (address != 0 || numBytes == 0)
            {
                return 0;
            }

            return Value;
        }
    }
}
