using Discord;
using Discord.Net;
using Discord.Commands;
using System.Linq;
using System;
using Discord.Audio;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CSGO_Bot {
    internal class Csgobot {

        DiscordClient discord;
        CommandService commands;

        Dictionary<String, String> compChannels = new Dictionary<String, String>();

        public Csgobot() {
            discord = new DiscordClient(x => {
                x.LogLevel = LogSeverity.Debug;
                x.LogHandler = Logger;
                
            });

            discord.UsingAudio(x => {
                x.Mode = AudioMode.Outgoing;
            });

            discord.MessageReceived += Discord_MessageReceived;
            discord.UserUpdated += Discord_UserUpdated;
            discord.ChannelDestroyed += Discord_ChannelDestroyed;

            discord.UsingCommands(x => {
                x.PrefixChar = '?';
                x.AllowMentionPrefix = true;
            });
            
            discord.ExecuteAndWait(async () => {
                await discord.Connect("MzI0NjUwNTg2NzY1MjYyODQ5.DCM1ew.55_LKXgwaLPACqwChIL9oegNYtk", TokenType.Bot);
            });

        }

        private async void Discord_ChannelDestroyed(object sender, ChannelEventArgs e) {
            if (compChannels.ContainsKey(e.Channel.Name)) {
                compChannels.Remove(e.Channel.Name);
            }

        }

        private async void Discord_UserUpdated(object sender, UserUpdatedEventArgs e) {
            //Check if user was in a comp voice channel, and is owner
            if (e.Before.VoiceChannel != null) {
                if (compChannels.ContainsKey(e.Before.VoiceChannel.Name) && compChannels[e.Before.VoiceChannel.Name] == e.Before.Name) {
                    //check if user changed voice channel
                    if (e.After.VoiceChannel == null || e.After.VoiceChannel != e.Before.VoiceChannel) {
                        compChannels.Remove(e.Before.VoiceChannel.Name);
                        await e.Before.VoiceChannel.Delete();
                    }
                }
            }
        }

        private async void Discord_MessageReceived(object sender, MessageEventArgs e) {
            if (!e.Message.IsAuthor) {
                
                string[] message = e.Message.RawText.Split(new char[] { ' ', ',' });

                //CreateLobby
                if (message[0].ToLower() == "?createlobby") {
                    await HandleCreateLobby(e, message);
                }
                //Shun
                if (message[0].ToLower() == "?shun") {
                    await HandleShun(e, message);
                }
                if (message[0].ToLower() == "?unshun") {
                    await HandleUnshun(e, message);
                }
            }
        }

        private async Task HandleUnshun(MessageEventArgs e, string[] message) {
            if (!(e.User.HasRole(e.Server.FindRoles("Admin").First()) || e.User.HasRole(e.Server.FindRoles("King").First()))) {
                await e.Channel.SendMessage("You cannot do that!");
            } else if (message.Count() !=  2) {
                await e.Channel.SendMessage("Usage: ?Unshun {User Name}");
            } else {
                string name = message[1];
                var search = e.Server.FindUsers(name);
                if (search.Count() == 0) {
                    await e.Channel.SendMessage("User not found!");
                } else {
                    var user = search.First();
                    var role = e.Server.FindRoles("Shunned").First();
                    if (!(user.HasRole(role) || user.VoiceChannel == e.Server.FindChannels("Shunned").First())) {
                        await e.Channel.SendMessage("That user is not shunned!");
                    } else {
                        await Unshun(user, e);
                    }
                }
            }
        }

        private async Task HandleCreateLobby(MessageEventArgs e, string[] message) {
            string name = string.Join(" ", message.Skip(1));
            if (string.Join("", name).Length > 30) {
                await e.Channel.SendMessage("Channel name too long!");
            } else if (message.Count() == 1) {
                await e.Channel.SendMessage("Usage: ?CreateLobby {Lobby Name}");
            } else if (e.Server.FindChannels(name, exactMatch: true).Count() > 0) {
                await e.Channel.SendMessage("Channel Name Exists");
            } else if (compChannels.ContainsValue(e.User.Name)) {
                await e.Channel.SendMessage("You already have an active channel! Leave it to delete.");
            } else if (!e.User.HasRole(e.Server.FindRoles("CS:GO").First())) {
                await e.Channel.SendMessage("You do not have the CS:GO role!");
            } else {
                //Create new channel
                var newChannel = await e.Server.CreateChannel(name, ChannelType.Voice);
                await newChannel.Edit(position: 4, topic: e.User.Name + "'s channel");
                //Move user to channel
                await e.User.Edit(voiceChannel: newChannel);
                //Give user permissions in channel
                await newChannel.AddPermissionsRule(e.User, new ChannelPermissionOverrides(moveMembers: PermValue.Allow, connect: PermValue.Allow, muteMembers: PermValue.Allow, managePermissions: PermValue.Allow, manageChannel: PermValue.Allow));
                await newChannel.AddPermissionsRule(e.Server.EveryoneRole, new ChannelPermissionOverrides(moveMembers: PermValue.Deny, connect: PermValue.Deny, muteMembers: PermValue.Deny, managePermissions: PermValue.Deny, manageChannel: PermValue.Deny));
                //Add channel to list, and user as owner
                compChannels.Add(newChannel.Name, e.User.Name);
            }
        }

        private async Task HandleShun(MessageEventArgs e, string[] message) {
            if (!(e.User.HasRole(e.Server.FindRoles("Admin").First()) || e.User.HasRole(e.Server.FindRoles("King").First()))) {
                await e.Channel.SendMessage("You cannot do that!");
            } else if (message.Count() != 3) {
                await e.Channel.SendMessage("Usage: ?Shun {User Name} {Time (min) | 0 = Indefinite}");
            } else {

                string name = message[1];
                string times = message[2];
                TimeSpan time = TimeSpan.FromMinutes(double.Parse(times));

                var search = e.Server.FindUsers(name, false);
                if (search.Count() == 0) {
                    await e.Channel.SendMessage("User not found!");

                } else if (search.First().HasRole(e.Server.FindRoles("King").First())) {
                    await e.Channel.SendMessage("You cannot shun that user!");
                } else if (search.First().HasRole(e.Server.FindRoles("Admin").First())) {
                    if (!e.User.HasRole(e.Server.FindRoles("King").First())) {
                        await e.Channel.SendMessage("You cannot shun that user!");
                    } else {
                        await Shun(search.First(), e);
                    }
                } else {
                    await Shun(search.First(), e);


                    if (times != "0") {

                        await Task.Delay(time);

                        await Unshun(search.First(), e);
                    }
                }
            }
        }

        private async Task Unshun(User user, MessageEventArgs e) {
            var channel = e.Server.FindChannels("Shunned").First();
            var role = e.Server.FindRoles("Shunned").First();
            var beforechannel = user.VoiceChannel;

            if (beforechannel.Name == "Shunned") {
                beforechannel = e.Server.FindChannels("General", type: ChannelType.Voice).First();
            }
            try {
                await user.Edit(voiceChannel: beforechannel);
            } catch {
                await user.Edit(voiceChannel: null);
            }
            
            await user.RemoveRoles(role);
            await e.Channel.SendMessage(user.Mention + ", your time is up!");
        }

        private async Task Shun(User user, MessageEventArgs e) {
            var channel = e.Server.FindChannels("Shunned").First();
            var role = e.Server.FindRoles("Shunned").First();
            var beforechannel = user.VoiceChannel;

            await user.Edit(voiceChannel: channel);
            await user.AddRoles(role);
            await e.Channel.SendMessage(user.Mention + ", You have been shunned!");
        }


        private void Logger(object sender, LogMessageEventArgs e) {
            System.Console.WriteLine(e.Message);
        }

    }
}