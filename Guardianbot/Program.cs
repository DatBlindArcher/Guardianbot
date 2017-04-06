using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DSharpPlus;
using HtmlAgilityPack;

namespace GuardianBot
{
    class Program
    {
        public static DiscordClient client;
        public static DiscordGuild guild;
        public static Dictionary<ulong, string> players;
        public static Dictionary<string, ulong> roles;
        public static Dictionary<ulong, string> modes;
        public static SortedList<DateTime, ulong> timeouts;
        public static string lastNews;
        public static string[] filter;
        public static ulong mode;
        public static bool inmode;
        public static Thread updater;

        static bool ReadNext()
        {
            string raw = Console.ReadLine();
            string[] rsplit = raw.Split(' ');
            string[] split = raw.ToLower().Split(' ');

            switch (split[0])
            {
                case "hello":
                    client.Guilds.Values.First().Channels[0].SendMessage("Hello World!");
                    break;

                case "reload":
                    reload();
                    break;

                case "say":
                    string message = "";
                    for (int i = 2; i < split.Length; i++) message += rsplit[i] + " ";
                    client.Guilds.Values.First().Channels.Where(x => x.Name == split[1]).FirstOrDefault().SendMessage(message);
                    break;

                case "help":
                    Console.WriteLine("hello");
                    Console.WriteLine("say <channelname> <text>");
                    Console.WriteLine("clean");
                    Console.WriteLine("reload");
                    Console.WriteLine("exit");
                    break;

                case "clean":
                    Clean();
                    break;

                case "exit":
                    return false;
            }

            return true;
        }

        static void Main(string[] args)
        {
            inmode = false;
            lastNews = "http://aq3d.com/news/ui3/";
            players = new Dictionary<ulong, string>();
            modes = new Dictionary<ulong, string>();
            timeouts = new SortedList<DateTime, ulong>();
            Console.Title = "Guardian Spirit";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Connecting ...");

            client = new DiscordClient(new DiscordConfig()
            {
                Token = File.ReadAllText("token.txt"),
                AutoReconnect = true
            });

            client.Connect();
            Console.WriteLine("Connected!");
            Console.WriteLine("Getting database ...");
            loadPlayers();
            loadRoles();
            reload();
            updater = new Thread(UpdateBot);
            updater.Start();

            client.GuildMemberAdd += async e =>
            {
                await SendMessage2(client.Guilds.Values.First().Channels[0], "Everyone welcome " + e.Member.User.Mention);
            };

            client.MessageCreated += async e =>
            {
                if (!e.Message.Author.IsBot)
                {
                    foreach (string f in filter)
                    {
                        if (e.Message.Content.ToLower().Contains(f.ToLower()))
                        {
                            await e.Message.Delete();
                            await SendMessage2(e.Channel, e.Message.Author.Username + ", watch your language");
                            break;
                        }
                    }
                }

                if (!e.Message.Author.IsBot && e.Message.Content.StartsWith("!"))
                {
                    try
                    {
                        await HandleCommands(e);
                    }

                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        await SendMessage2(e.Channel, e.Message.Author.Username + ", dont try to crash me");
                    }
                }
            };
            
            Console.WriteLine("The bot is running, you can type commands now.");
            while (ReadNext());
            Console.WriteLine("Press anything to close ...");
            Console.ReadKey();
            client.Disconnect();
            client.Dispose();
            savePlayers();
        }

        private static void UpdateBot()
        {
            Thread.Sleep(30000);

            if (timeouts.Count > 0)
            {
                while (timeouts.Keys[0] < DateTime.Now)
                {
                    untimeout(timeouts.Values[0]);
                    timeouts.Remove(timeouts.Keys[0]);
                }
            }

            checkNews();
        }

        private static async void Clean()
        {
            DiscordGuild guild = client.Guilds[289077903403122689];
            DiscordMember[] members = guild.Members.ToArray();

            foreach (DiscordMember member in members)
            {
                if (!players.ContainsKey(member.User.ID))
                {
                    if (!member.Roles.Contains(roles["Guardian Admin"]))
                    {
                        List<ulong> result = member.Roles;
                        if (member.Roles.Contains(roles["Guardian"]))
                            result.Remove(roles["Guardian"]);
                        if (member.Roles.Contains(roles["Dragon Guardian"]))
                            result.Remove(roles["Dragon Guardian"]);
                        await guild.ModifyMember(member.User.ID, null, result, false, false, 0);
                    }
                }

                Thread.Sleep(1000);
            }
        }

        private static void loadPlayers()
        {
            if (File.Exists("players.gdb"))
            {
                string[] lines = File.ReadAllLines("players.gdb");

                for (int i = 0; i < lines.Length; i++)
                {
                    try
                    {
                        string[] values = lines[i].Split('#');
                        players.Add(ulong.Parse(values[0]), values[1]);
                    }

                    catch (Exception e)
                    {
                        Console.WriteLine(e.GetType() + " from line " + i + " with values: " + lines[i]);
                    }
                }
            }

            if (File.Exists("modes.gdb"))
            {
                string[] lines = File.ReadAllLines("modes.gdb");

                for (int i = 0; i < lines.Length; i++)
                {
                    try
                    {
                        string[] values = lines[i].Split('#');
                        string mode = values[1];
                        for (int j = 1; j < values.Length; j++)
                            mode += "#" + values[j];
                        modes.Add(ulong.Parse(values[0]), mode);
                    }

                    catch (Exception e)
                    {
                        Console.WriteLine(e.GetType() + " from line " + i + " with values: " + lines[i]);
                    }
                }
            }

            if (File.Exists("timeouts.gdb"))
            {
                string[] lines = File.ReadAllLines("timeouts.gdb");
                lastNews = lines[0];

                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        string[] values = lines[i].Split('#');
                        timeouts.Add(DateTime.FromBinary(long.Parse(values[0])), ulong.Parse(values[1]));
                    }

                    catch (Exception e)
                    {
                        Console.WriteLine(e.GetType() + " from line " + i + " with values: " + lines[i]);
                    }
                }
            }
        }

        private static void reload()
        {
            if (File.Exists("filter.txt"))
            {
                filter = File.ReadAllLines("filter.txt");
            }
        }

        private static async void loadRoles()
        {
            roles = new Dictionary<string, ulong>();
            guild = await client.GetGuild(289077903403122689);

            foreach (DiscordRole role in guild.Roles)
            {
                roles.Add(role.Name, role.ID);
            }
        }

        private static void savePlayer(ulong key, string name)
        {
            StreamWriter writer = new StreamWriter("players.gdb", true);
            writer.WriteLine(key + "#" + name);
            writer.Close();
        }

        private static void savePlayers()
        {
            StreamWriter writer = new StreamWriter("players.gdb");

            foreach (KeyValuePair<ulong, string> kv in players)
            {
                writer.WriteLine(kv.Key + "#" + kv.Value);
            }

            writer.Close();
        }

        private static void saveMode(ulong key, string mode)
        {
            StreamWriter writer = new StreamWriter("modes.gdb", true);
            writer.WriteLine(key + "#" + mode);
            writer.Close();
        }

        private static void saveModes()
        {
            StreamWriter writer = new StreamWriter("modes.gdb");

            foreach (KeyValuePair<ulong, string> kv in modes)
            {
                writer.WriteLine(kv.Key + "#" + kv.Value);
            }

            writer.Close();
        }

        private static void saveTimeouts()
        {
            StreamWriter writer = new StreamWriter("timeouts.gdb");
            writer.WriteLine(lastNews);

            foreach (KeyValuePair<DateTime, ulong> kv in timeouts)
            {
                writer.WriteLine(kv.Key.ToBinary() + "#" + kv.Value);
            }

            writer.Close();
        }

        public static async Task SendMessage2(DiscordChannel channel, string message)
        {
            await channel.SendMessage(message + (inmode ? ", " + modes[mode] : "."), false, null);
        }

        private static async Task HandleCommands(MessageCreateEventArgs e)
        {
            string[] arguments = e.Message.Content.Split(' ');

            switch (arguments[0].TrimStart('!').ToLower())
            {
                case "badges":
                    {
                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <discordaccount>): !badges <discordaccount>");
                            return;
                        }

                        else
                        {
                            if (!arguments[1].Contains('@'))
                            {
                                await SendMessage2(e.Channel, "That's not a discord account");
                                return;
                            }

                            if (!players.ContainsKey(getUserID(arguments[1])))
                                await SendMessage2(e.Channel, "Player not set yet");
                            List<string> badges = getCharacterBadges(players[getUserID(arguments[1])]);
                            if (badges == null)
                                await SendMessage2(e.Channel, "Player not found");
                            else
                            {
                                string message = "You have the following badges:\n";
                                foreach (string badge in badges) message += badge + "\n";
                                await SendMessage2(e.Channel, message);
                            }
                        }
                    }
                    break;

                case "dragonguardian":
                    {
                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <discordaccount>): !dragonguardian <discordaccount>");
                            return;
                        }

                        else
                        {
                            if (!arguments[1].Contains('@'))
                            {
                                await SendMessage2(e.Channel, "That's not a discord account");
                                return;
                            }

                            if (!players.ContainsKey(getUserID(arguments[1])))
                                await SendMessage2(e.Channel, "Player not set yet");
                            string name = players[getUserID(arguments[1])];
                            List<string> badges = getCharacterBadges(name);
                            if (badges == null)
                                await SendMessage2(e.Channel, "Player not found");
                            else
                                await SendMessage2(e.Channel, name.Replace("%20", " ") + "is " + (badges.Contains("Collector's Guardian") ? "" : "not ") + "a dragon guardian");
                        }
                    }
                    break;

                case "showitem":
                    {
                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <item name>): !showitem <item name>");
                            return;
                        }

                        else
                        {
                            string name = "";
                            for (int i = 1; i < arguments.Length; i++) name += arguments[i].ToLower() + "%20";
                            string link = getItemImage(name);

                            if (link.Contains("http"))
                                await SendMessage2(e.Channel, "<" + link + ">");
                            else
                                await SendMessage2(e.Channel, link);
                        }
                    }
                    break;

                case "itemstats":
                    {
                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <item name>): !itemstats <item name>");
                            return;
                        }

                        else
                        {
                            string name = "";
                            for (int i = 1; i < arguments.Length; i++) name += arguments[i].ToLower() + "%20";
                            string stats = getItemStats(name);
                            await SendMessage2(e.Channel, stats);
                        }
                    }
                    break;

                case "designnotes":
                    {
                        await SendMessage2(e.Channel, "http://www.aq3d.com/patchnotes/");
                    }
                    break;

                case "help":
                    {
                        await SendMessage2(e.Channel, "Current commands:\n!badges <discordaccount>\n!dragonguardian <discordaccount>\n!showitem <item name>\n!itemstats <item name>\n!designnotes\n!setplayer <discordaccount> <aq3d name>\n!getplayer <discordaccount>\n!updateplayer <discordaccount>\n!rules\n!setmode <discordaccount> <content>");
                    }
                    break;

                case "setplayer":
                    {
                        if (!(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Guardian Admin"])
                            && !(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Leader"]))
                            return;
                        
                        if (arguments.Length < 3)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <#>): !setplayer <discordaccount> <aq3d name>");
                            return;
                        }

                        if (!arguments[1].Contains('@'))
                        {
                            await SendMessage2(e.Channel, "That's not a discord account");
                            return;
                        }

                        if (players.ContainsKey(getUserID(arguments[1])))
                        {
                            await SendMessage2(e.Channel, "Player has already been set");
                            return;
                        }

                        string name = "";
                        for (int i = 2; i < arguments.Length; i++) name += arguments[i].ToLower() + "%20";
                        ulong playerID = getUserID(arguments[1]);
                        players.Add(playerID, name);
                        DiscordMember member = await e.Guild.GetMember(playerID);

                        if (!member.Roles.Contains(roles["Guardian Admin"]))
                        {
                            List<string> badges = getCharacterBadges(name);
                            if (badges.Contains("Guardian"))
                            {
                                List<ulong> result = member.Roles;
                                result.Add(roles["Guardian"]);

                                if (badges.Contains("Collector's Guardian"))
                                {
                                    await SendMessage2(e.Channel, "Player is a dragon guardian");
                                    result.Add(roles["Dragon Guardian"]);
                                }

                                else
                                {
                                    await SendMessage2(e.Channel, "Player is a guardian");
                                }

                                await e.Guild.ModifyMember(member.User.ID, null, result, false, false, 0);
                            }

                            else
                            {
                                await SendMessage2(e.Channel, "Player is not a guardian");
                                players.Remove(getUserID(arguments[1]));
                            }
                        }

                        savePlayer(getUserID(arguments[1]), name);
                    }
                    break;

                case "updateplayer":
                    {
                        if (!(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Guardian Admin"])
                            && !(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Leader"]))
                            return;

                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <#>): !updateplayer <discordaccount>");
                            return;
                        }

                        if (!arguments[1].Contains('@'))
                        {
                            await SendMessage2(e.Channel, "That's not a discord account");
                            return;
                        }

                        ulong playerID = getUserID(arguments[1]);
                        DiscordMember member = await e.Guild.GetMember(playerID);

                        if (!member.Roles.Contains(roles["Guardian Admin"]))
                        {
                            List<string> badges = getCharacterBadges(players[playerID]);
                            if (badges.Contains("Guardian"))
                            {
                                List<ulong> result = member.Roles;
                                if (!result.Contains(roles["Guardian"]))
                                    result.Add(roles["Guardian"]);

                                if (badges.Contains("Collector's Guardian"))
                                {
                                    await SendMessage2(e.Channel, "Player is a dragon guardian.");
                                    if (!result.Contains(roles["Dragon Guardian"]))
                                        result.Add(roles["Dragon Guardian"]);
                                }

                                else
                                {
                                    await SendMessage2(e.Channel, "Player is a guardian");
                                }

                                await e.Guild.ModifyMember(member.User.ID, null, result, false, false, 0);
                            }

                            else
                            {
                                await SendMessage2(e.Channel, "Player is not a guardian");
                                players.Remove(getUserID(arguments[1]));
                            }
                        }
                    }
                    break;

                case "delplayer":
                    {
                        if (!(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Guardian Admin"])
                            && !(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Leader"]))
                            return;

                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <#>): !delplayer <discordaccount>");
                            return;
                        }

                        if (!arguments[1].Contains('@'))
                        {
                            await SendMessage2(e.Channel, "That's not a discord account");
                            return;
                        }

                        ulong playerID = getUserID(arguments[1]);
                        DiscordMember member = await e.Guild.GetMember(playerID);

                        if (!member.Roles.Contains(roles["Guardian Admin"]))
                        {
                            List<ulong> result = member.Roles;
                            if (member.Roles.Contains(roles["Guardian"]))
                                result.Remove(roles["Guardian"]);
                            if (member.Roles.Contains(roles["Dragon Guardian"]))
                                result.Remove(roles["Dragon Guardian"]);
                            await guild.ModifyMember(member.User.ID, null, result, false, false, 0);
                        }

                        if (!players.ContainsKey(playerID))
                        {
                            await SendMessage2(e.Channel, arguments[1] + " has not a set player yet");
                            return;
                        }

                        players.Remove(getUserID(arguments[1]));
                        savePlayers();
                    }
                    break;

                case "getplayer":
                    {
                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <#>): !getplayer <discordaccount>");
                            return;
                        }

                        if (!arguments[1].Contains('@'))
                        {
                            await SendMessage2(e.Channel, "That's not a discord account");
                            return;
                        }

                        if (!players.ContainsKey(getUserID(arguments[1])))
                        {
                            await SendMessage2(e.Channel, arguments[1] + " has not a set player yet");
                            return;
                        }

                        await SendMessage2(e.Channel, arguments[1].ToLower() + " is player " + players[getUserID(arguments[1])].Replace("%20", " "));
                    }
                    break;

                case "rules":
                    {
                        await SendMessage2(e.Channel, "<#293380266502651904>");
                    }
                    break;

                case "timeout":
                    {
                        if (arguments.Length < 2)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <#>): !timeout <discordaccount>");
                            return;
                        }

                        if (!arguments[1].Contains('@'))
                        {
                            await SendMessage2(e.Channel, "That's not a discord account");
                            return;
                        }

                        if (!(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Guardian Admin"]))
                            return;

                        int duration = 500;
                        if (arguments.Length > 2)
                            duration = int.Parse(arguments[2]);

                        ulong playerID = getUserID(arguments[1]);
                        DiscordMember member = await e.Guild.GetMember(playerID);
                        await SendMessage2(e.Channel, arguments[1] + " is timed out for " + duration + " seconds");
                        timeout(member, duration);
                    }
                    break;

                case "setmode":
                    {
                        if (arguments.Length < 3)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <#>): !setmode <discordaccount> <content>");
                            return;
                        }

                        if (!arguments[1].Contains('@'))
                        {
                            await SendMessage2(e.Channel, "That's not a discord account");
                            return;
                        }

                        ulong playerID = getUserID(arguments[1]);

                        if (!players.ContainsKey(playerID))
                        {
                            await SendMessage2(e.Channel, "Player is not a member");
                            return;
                        }

                        string m = "";
                        for (int i = 2; i < arguments.Length; i++) m += arguments[i] + " ";

                        if (modes.ContainsKey(playerID))
                        {
                            modes[playerID] = m;
                            saveModes();
                        }

                        else
                        {
                            modes.Add(playerID, m);
                            saveMode(playerID, m);
                        }

                        await SendMessage2(e.Channel, "Mode has been set");
                    }
                    break;

                case "mode":
                    {
                        if (arguments.Length < 3)
                        {
                            await SendMessage2(e.Channel, "The command is (fill in the <#>): !mode <discordaccount> <on/off>");
                            return;
                        }

                        if (!(await e.Guild.GetMember(e.Message.Author.ID)).Roles.Contains(roles["Guardian Admin"]))
                            return;

                        if (!arguments[1].Contains('@'))
                        {
                            await SendMessage2(e.Channel, "That's not a discord account");
                            return;
                        }

                        mode = getUserID(arguments[1]);

                        if (!players.ContainsKey(mode))
                        {
                            await SendMessage2(e.Channel, "That is not a member");
                            return;
                        }

                        if (!modes.ContainsKey(mode))
                        {
                            await SendMessage2(e.Channel, "Member has not a set mode");
                            return;
                        }
                        
                        if (arguments[2] == "on")
                        {
                            inmode = true;
                            await SendMessage2(e.Channel, "I will now talk like my one and only " + players[mode].Replace("%20", " "));
                        }

                        else
                        {
                            inmode = false;
                            await client.Guilds.Values.First().Channels[0].SendMessage("I'm now back to a normal transcendent Guardian Bot.");
                        }
                    }
                    break;

                default:
                    await SendMessage2(e.Channel, "Unknown command");
                    break;
            }
        }

        private static async void timeout(DiscordMember member, int duration)
        {
            if (!member.Roles.Contains(roles["Guardian Admin"]))
            {
                List<ulong> result = member.Roles;
                if (!member.Roles.Contains(roles["Blocked"]))
                    result.Add(roles["Blocked"]);
                await guild.ModifyMember(member.User.ID, null, result, false, false, 0);
            }

            timeouts.Add(DateTime.Now.AddSeconds(duration), member.User.ID);
            saveTimeouts();
        }

        private static async void untimeout(ulong memberID)
        {
            DiscordMember member = await client.Guilds[289077903403122689].GetMember(memberID);

            if (!member.Roles.Contains(roles["Guardian Admin"]))
            {
                List<ulong> result = member.Roles;
                if (member.Roles.Contains(roles["Blocked"]))
                    result.Remove(roles["Blocked"]);
                await guild.ModifyMember(member.User.ID, null, result, false, false, 0);
            }
        }

        private static List<string> getCharacterBadges(string name)
        {
            List<string> badges = new List<string>();
            string html;

            try
            {
                using (var client = new WebClient())
                {
                    html = client.DownloadString("http://account.aq3d.com/Character?id=" + name);
                }
            }

            catch
            {
                return null;
            }

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            if (doc.DocumentNode.SelectSingleNode("//div[@id='Loyalty']") == null)
                return new List<string>(new string[1] { "No badges found" });
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//div[@id='Loyalty']/div[@class='row']//h3");

            foreach (HtmlNode node in nodes)
            {
                badges.Add(WebUtility.HtmlDecode(node.InnerText));
            }

            return badges;
        }
        
        private static ulong getUserID(string discordtag)
        {
            return ulong.Parse(discordtag.Trim('@', '<', '>', '!'));
        }

        private static string getItemImage(string itemname)
        {
            string html;

            try
            {
                using (var client = new WebClient())
                {
                    html = client.DownloadString("http://aq-3d.wikidot.com/" + itemname);
                }
            }

            catch
            {
                return "Item not found";
            }

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            if (doc.DocumentNode.SelectSingleNode("//div[@id='page-content']/div") != null)
                if (doc.DocumentNode.SelectSingleNode("//div[@id='page-content']/div[last()]//img[last()]") == null)
                    return "No image found";
                else
                    return doc.DocumentNode.SelectSingleNode("//div[@id='page-content']/div[last()]//img[last()]").GetAttributeValue("src", "No image found");
            if (doc.DocumentNode.SelectSingleNode("//div[@id='page-content']//img[last()]") == null)
                return "No image found";
            return doc.DocumentNode.SelectSingleNode("//div[@id='page-content']//img[last()]").GetAttributeValue("src", "No image found");
        }

        private static string getItemStats(string itemname)
        {
            string html;

            try
            {
                using (var client = new WebClient())
                {
                    html = client.DownloadString("http://aq-3d.wikidot.com/" + itemname);
                }
            }

            catch
            {
                return "Item not found";
            }

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (doc.DocumentNode.SelectSingleNode("//table[@class='wiki-content-table']") == null)
                return "Item has no stats";

            string message = "";
            HtmlNodeCollection stats = doc.DocumentNode.SelectNodes("//table[@class='wiki-content-table']//td");

            for (int i = 0; i < stats.Count; i += 2)
            {
                message += WebUtility.HtmlDecode(stats[i].InnerHtml) + ": " + WebUtility.HtmlDecode(stats[i + 1].InnerHtml) + "\n";
            }

            return message.TrimEnd('\n');
        }

        private static async void checkNews()
        {
            string html;

            try
            {
                using (var client = new WebClient())
                {
                    html = client.DownloadString("http://aq3d.com/");
                }
            }

            catch
            {
                return;
            }

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//div[@class='caption']/a");
            DiscordChannel news = client.Guilds[289077903403122689].Channels.Where(x => x.ID == 289567617767833600).FirstOrDefault();

            foreach (HtmlNode node in nodes)
            {
                string tNews = "http://aq3d.com" + node.GetAttributeValue("href", "");

                if (lastNews == tNews)
                    break;

                lastNews = tNews;
                await news.SendMessage(tNews);
                saveTimeouts();
            }
        }
    }
}