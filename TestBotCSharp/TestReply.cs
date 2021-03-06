﻿using Microsoft.Bot.Connector;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using Microsoft.Bot.Connector.Teams.Models;
using Microsoft.Bot.Connector.Teams;
using Microsoft.Bot.Builder.Dialogs;

namespace TestBotCSharp
{

    public class TestReply : TestBotReply
    {
        private static string S_STANDARD_IMGURL = "https://skypeteamsbotstorage.blob.core.windows.net/bottestartifacts/panoramic.png";
        private static string S_THUMB_IMGURL = "https://skypeteamsbotstorage.blob.core.windows.net/bottestartifacts/sandwich_thumbnail.png";



        private int m_dumpRequested = 0;
            static private int DUMPIN = 1;
            static private int DUMPOUT = 2;
            static private char CHAR_DUMP = '|';

        
        //Dictionary - this is a combo of the about string and the command to run.
        public struct TestDetail
        {
            public string about;
            public delegate Task<bool> buildMessage();
            public buildMessage func;
            public bool isAsync;

            public TestDetail(string x, buildMessage y, bool z = false)
            {
                about = x;
                func = y;
                isAsync = z;
            }

        }

        private  Dictionary<string, TestDetail> m_cmdToTestDetail;
        public TestReply(ConnectorClient c) : base(c)
        {
            CreateCommandList();
        }
        public TestReply(IDialogContext c) : base(c)
        {
            CreateCommandList();

        }
        private void CreateCommandList()
        { 
            m_cmdToTestDetail = new Dictionary<string, TestDetail>(StringComparer.InvariantCultureIgnoreCase);
            m_cmdToTestDetail.Add("help", new TestDetail("Show this message", HelpMessage));
            
            m_cmdToTestDetail.Add("!help", new TestDetail("!Show all commands, including hidden", HelpVerbose));

            m_cmdToTestDetail.Add("hero1", new TestDetail("Hero Card with [3] buttons", Hero1Message));
            m_cmdToTestDetail.Add("hero2", new TestDetail("!Hero Card with no image and [3] buttons", Hero2Message));
            m_cmdToTestDetail.Add("hero3", new TestDetail("!Hero Card with no content and [\"Optional Title\"]", Hero3Message));
            m_cmdToTestDetail.Add("hero4", new TestDetail("!Hero Card with no content and [\"Optional Title\"]", Hero4Message));
            m_cmdToTestDetail.Add("imgCard", new TestDetail("Hero Card with [\"img\"] as Content", ImgCardMessage));
            m_cmdToTestDetail.Add("heroRYO", new TestDetail("Roll your own: [\"Title\"] [\"SubTitle\"] [\"Content\"] [\"ImageURL\"] [ImBack Button count] ", HeroRYOMessage));

            m_cmdToTestDetail.Add("heroInvoke", new TestDetail("Hero Card with [2] buttons using invoke action type", HeroInvokeMessage));

            m_cmdToTestDetail.Add("carousel1", new TestDetail("Show a Carousel with different cards in each", Carousel1Message));
            m_cmdToTestDetail.Add("carouselx", new TestDetail("Show a Carousel with [5] identical cards", CarouselxMessage));

            m_cmdToTestDetail.Add("list1", new TestDetail("Show a List with different cards in each", List1Message));
            m_cmdToTestDetail.Add("listx", new TestDetail("Show a List with [5] identical cards", ListxMessage));

            m_cmdToTestDetail.Add("thumb", new TestDetail("Display a Thumbnail Card", ThumbnailMessage));
            m_cmdToTestDetail.Add("thumblist", new TestDetail("Show a List with [5] identical thumbnails", ThumbnailListMessage));
            m_cmdToTestDetail.Add("thumbRYO", new TestDetail("Roll your own: [\"Title\"] [\"SubTitle\"] [\"Content\"] [\"ImageURL\"] [ImBack Button count] ", HeroRYOMessage));

            m_cmdToTestDetail.Add("connectorcard", new TestDetail("!Connector Card", ConnectorCardTest));

            m_cmdToTestDetail.Add("animcard", new TestDetail("!Display an Animation Card - not supported", AnimationCardMessage));
            m_cmdToTestDetail.Add("videocard", new TestDetail("!Display a Video Card - not supported", VideoCardMessage));
            m_cmdToTestDetail.Add("audiocard", new TestDetail("!Display an Audio Card - not supported", AudioCardMessage));
            
            m_cmdToTestDetail.Add("getattach", new TestDetail("!Send an inline attachment (img, gif) to your bot", GetAttachMessage));
            
            m_cmdToTestDetail.Add("update", new TestDetail("Update a message", UpdateMessage, true));

            m_cmdToTestDetail.Add("signin", new TestDetail("Show a Signin Card, with button to launch [URL]",SignInMessage));
            m_cmdToTestDetail.Add("formatxml", new TestDetail("Display a [\"sample\"] selection of XML formats", FormatXMLMessage));
            m_cmdToTestDetail.Add("formatmd", new TestDetail("Display a [\"sample\"] selection of Markdown formats", FormatMDMessage));

            m_cmdToTestDetail.Add("echo", new TestDetail("Echo your [\"string\"]", EchoMessage));
            m_cmdToTestDetail.Add("mentions", new TestDetail("Show the @mentions you pass", MentionsTest));
            m_cmdToTestDetail.Add("mentionUser", new TestDetail("@mentions the passed user", MentionUser));

            m_cmdToTestDetail.Add("members", new TestDetail("Show members of the team", MembersTest, true));

            m_cmdToTestDetail.Add("create", new TestDetail("Create a new conversation in channel", CreateConversation));
            m_cmdToTestDetail.Add("create11", new TestDetail("Create a new 1:1 conversation (send message to you)", Create11Conversation));

            m_cmdToTestDetail.Add("channels", new TestDetail("Show all channels in the team", ChannelsTest));

            m_cmdToTestDetail.Add("imback", new TestDetail("!This is just a handler for the imback buttons", ImBackResponse));

            m_cmdToTestDetail.Add("dumpin", new TestDetail("Display the incoming JSON", ActivityDumpIn));
            m_cmdToTestDetail.Add("dumpout", new TestDetail("Display the outgoing JSON", ActivityDumpOut));

            m_cmdToTestDetail.Add("suggested", new TestDetail("!Suggested Action test", SuggestedActionTest));
            m_cmdToTestDetail.Add("inputhint", new TestDetail("!InputHints test", InputHintsTest));
        }

        /// <summary>
        /// Convert emoji code into surrogate pair
        /// </summary>
        /// <param name="emoji"></param>
        /// <returns></returns>
        private string EmojiToSurrogatePair(int emoji)
        {
            double H = Math.Floor((double)((emoji - 0x10000) / 0x400)) + 0xD800;
            double L = ((emoji - 0x10000) % 0x400) + 0xDC00;
            char ch = (char)H;
            char cl = (char)L;

            return ch.ToString() + cl.ToString();
        }

        /// <summary>
        /// Remove arg[0] from message string, which should be the command itself.  Used in EchoTest for ow
        /// </summary>
        /// <returns></returns>
        private string StripCommandFromMessage()
        {
            string message = m_sourceMessage.Text;

            message = message.Replace(m_args[0], "");

            return message;
        }

        /// <summary>
        /// Strips out the bot name, which is passed as part of message when bot is referenced in-channel.
        /// </summary>
        /// <param name="message">the Activity text</param>
        /// <returns>the message without the bot, if mentioned</returns>
        private string StripBotNameFromText (string message)
        {
            var messageText = message;

            Mention[] m = m_sourceMessage.GetMentions();

            for (int i = 0;i < m.Length;i++)
            {
                if (m[i].Mentioned.Id == m_sourceMessage.Recipient.Id)
                {
                    if (m[i].Text != null) //the Text field contains the full <at>name</at> string so is useful for stripping out.  If it's null, though, the bot name was passed silently, for e.g. bot-in-channel imBack
                        messageText = messageText.Replace(m[i].Text, "");
                }
            }

  
            return messageText;
        }

        private ConnectorClient getConnector()
        {
            return new ConnectorClient(new Uri(m_sourceMessage.ServiceUrl));
        }

        /// <summary>
        /// Safe return of arg# as String
        /// </summary>
        /// <param name="argnum"></param>
        /// <returns>String contained in arg# or null, if no arg</returns>
        private string GetArg(int argnum)
        {
            //Count 1 based, argnum is 0 based
            if (m_args.Count > argnum)
                return m_args[argnum];
            else
                return null;

        }


        /// <summary>
        /// Safe return of arg# as Int
        /// </summary>
        /// <param name="argnum"></param>
        /// <returns>Int contained in arg# or -1, if no arg</returns>
        private int GetArgInt(int argnum)
        {

            if (m_args.Count > argnum)
                return Convert.ToInt32(m_args[argnum]);
            else
                return -1;

        }

        /// <summary>
        /// Core dispatcher.  Parse the messageIn text, stripping out bot name if included and assuming the first string is the command.  Split rest of string into optional args for comands.
        /// </summary>
        /// <param name="messageIn"></param>
        /// <returns></returns>
        public override async Task<Activity> CreateMessage(Activity messageIn)
        {
            m_sourceMessage = messageIn; //Store off so we don't pass around



            //Create the message as a simple Reply
            m_replyMessage = messageIn.CreateReply();

            if (messageIn.Type == ActivityTypes.Invoke)
            {
                InvokeResponse();  
            }
            else
            {

                string messageText = StripBotNameFromText(messageIn.Text); //This will strip out the botname if the message came via channel and therefore it's mentioned

                //Split into arguments.  If in quotes, treat entire string as a single arg.
                m_args = messageText.Split('"')
                                     .Select((element, index) => index % 2 == 0  // If even index
                                                           ? element.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)  // Split the item
                                                           : new string[] { element })  // Keep the entire item
                                     .SelectMany(element => element).ToList();

                //one more pass to remove empties and whitespace
                m_args = m_args.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                for (int i = 0; i < m_args.Count; i++)
                {
                    m_args[i] = m_args[i].Trim();
                }

                if (m_args.Count > 0)
                {
                    string testcommand = m_args[0];
                    m_dumpRequested = 0;

                    //Scan for dump tag - DumpIn = prepend, DumpOut = postpend
                    if (testcommand[0] == CHAR_DUMP)
                    {
                        testcommand = testcommand.Substring(1);
                        m_dumpRequested += DUMPIN;
                    }

                    if (testcommand[testcommand.Length - 1] == CHAR_DUMP)
                    {
                        testcommand = testcommand.Remove(testcommand.Length - 1);
                        m_dumpRequested += DUMPOUT;
                    }


                    //Dispatch the command - check dictionary for command and run the appropriate function.
                    if (m_cmdToTestDetail.ContainsKey(testcommand))
                    {
                        // await Task.Run(() => m_cmdToTestDetail[testcommand].buildMessage());
                        //m_cmdToTestDetail[testcommand].buildMessage();
                        //var x = await GetAttachMessage();
                        //var x = await m_cmdToTestDetail[testcommand].buildMessage();
                        //IAsyncResult ar = m_cmdToTestDetail[testcommand].func.BeginInvoke(null, null);
                        //var x = m_cmdToTestDetail[testcommand].func.EndInvoke(ar);

                        if (m_cmdToTestDetail[testcommand].isAsync)
                        {
                            var x = await m_cmdToTestDetail[testcommand].func();
                        } 
                        else
                        {
                            var x = m_cmdToTestDetail[testcommand].func();
                        }
 
                    }
                    else
                    {
                        //m_dumpRequested = DUMPIN;
                        HelpMessage();
                    }

                }
                else
                {
                    HelpMessage();
                }
            }

            return m_replyMessage;
        }


        /// <summary>
        /// Show the payload for either the source or reply message.  The flag m_dumpRequested is set in the message parsing, based on the location of the pipe character.
        /// </summary>
        /// <param name="messageIn">The message that the bot received</param>
        /// <param name="messageOut">The test message that the bot created</param>
        /// <returns></returns>
        public override Activity DumpMessage(Activity messageIn, Activity messageOut)
        {

            if (m_dumpRequested == 0) return null;

            Activity temp = messageIn.CreateReply();

            temp.Text = "";

            if ((m_dumpRequested & 1) == 1)
            {
                temp.Text += "<b>ActivityIn:</b><br/>";
                temp.Text += ActivityDumper.ActivityDump(messageIn);
            }

            if (m_dumpRequested == 3) temp.Text += "<br />< hr ><br />"; //separator if both

            if ((m_dumpRequested & 2) == 2)
            {
                temp.Text += "<b>ActivityOut:</b><br/>";
                temp.Text += ActivityDumper.ActivityDump(messageOut);

            }
            
            temp.TextFormat = TextFormatTypes.Xml;

            return temp;

        }

        /// <summary>
        /// Show a list of all available commands
        /// </summary>
        /// <param name="showAll">set to True to show hidden tests as well</param>
        private void HelpDisplay (bool showAll = false)
        {
            var outText = "You entered [" + m_sourceMessage.Text + "]<br />";

#if false
            for (int i = 0; i < args.Count; i++)
            {
                outText += "<br />args[" + (i) + "] = [" + args[(i)] + "] - Leng: " + args[i].Length;
            }

#endif

            outText += "<br />** A list of all valid tests.** <br /> <br />  Values in [] can be changed by adding appropriate arguments, e.g. 'hero1 5' makes a hero1 card with 5 buttons; 'hero3 \"This is a Title\"' uses that string as the title.<br /> <br />You can prepend or postpend '|' (pipe) to dump the payload for incoming or outgoing message, respectively. <br /> <br /> ---";

            foreach(var item in m_cmdToTestDetail)
            {
                //If first char of Description is ! don't display it in help, unless command has ! as first char (e.g. !help).  So we can have hidden test cases.
                if (showAll || item.Value.about[0] != '!')
                    outText += "<br />**" + item.Key + "** - " + item.Value.about;
            }

            m_replyMessage.Text = outText;

        }

        private async Task<bool> HelpMessage()
        {
            HelpDisplay(false);
            return true;
        }
        private async Task<bool> HelpVerbose()
        {
            HelpDisplay(true);
            return true;
        }

        /// <summary>
        /// Simply pass in the source as the Activity dump source. This is for the appropriate command, not for |
        /// </summary>
        private async Task<bool> ActivityDumpIn ()
        {
            m_replyMessage.Text = ActivityDumper.ActivityDump(m_sourceMessage);
            m_replyMessage.TextFormat = TextFormatTypes.Xml;

            return true;
           
        }

        /// <summary>
        /// Simply pass in the reply as the Activity dump source.  This is for the appropriate command, not for |
        /// </summary>
        private async Task<bool> ActivityDumpOut()
        {
            m_replyMessage.Text = "Dump out text";
            m_replyMessage.Text = ActivityDumper.ActivityDump(m_replyMessage);
            m_replyMessage.TextFormat = TextFormatTypes.Xml;

            return true;
        }


        /// <summary>
        /// Helper function to create buttons using ImBack action
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private CardAction[] CreateImBackButtons(int num)
        {
            if (num < 1) return null;

            var buttons = new CardAction[num];
            for (int i = 0; i < num; i++)
            {
                buttons[i] = new CardAction()
                {
                    Title = "ImBack " + i,
                    Type = ActionTypes.ImBack,
                    Value = "ImBack " + i
                };
            }

            return buttons;
        }


        /// <summary>
        /// Helper function to create buttons using Invoke action
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        private CardAction[] CreateInvokeButtons(int num)
        {
            if (num < 1) return null;

            var buttons = new CardAction[num];
            for (int i = 0; i < num; i++)
            {
                buttons[i] = new CardAction()
                {
                    Title = "Invoke " + i,
                    Type = "invoke",
                    Value = "Good"
                    //Value = "{\"invokeValue:\": \"" + i + "\"}"
                };
            }

            return buttons;
        }




        /// <summary>
        /// Hero card using Invoke buttons
        /// </summary>
        private async Task<bool> HeroInvokeMessage()
        {
            int numberOfButtons = GetArgInt(1);
            if (numberOfButtons == -1) numberOfButtons = 2;

            m_replyMessage.Attachments = new List<Attachment>()
            {

                GetHeroCardAttachment(
                    "Invoke",
                    "Hero card with invoke buttons",
                    "Bacon ipsum dolor amet flank ground round chuck pork loin. Sirloin meatloaf boudin meatball ham hock shoulder capicola tri-tip sausage biltong cupim",
                    null, 
                    CreateInvokeButtons(numberOfButtons)
                )
            };
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> Hero1Message()
        {
            int numberOfButtons = GetArgInt(1);
            if (numberOfButtons == -1) numberOfButtons = 3;

            m_replyMessage.Attachments = new List<Attachment>()
            {

                GetHeroCardAttachment(
                    "Subject Title",
                    "Subtitle or breadcrumb",
                    "Bacon ipsum dolor amet flank ground round chuck pork loin. Sirloin meatloaf boudin meatball ham hock shoulder capicola tri-tip sausage biltong cupim",
                    new string[] { S_STANDARD_IMGURL },
                    CreateImBackButtons(numberOfButtons)
                )
            };

            return true;

        }


        private async Task<bool> Hero2Message()
        {
            int numberOfButtons = GetArgInt(1);
            if (numberOfButtons == -1) numberOfButtons = 3;


            //No Image, 3 buttons
            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetHeroCardAttachment(
                    "Subject Title",
                    "Subtitle or breadcrumb",
                    "Bacon ipsum dolor amet flank ground round chuck pork loin. Sirloin meatloaf boudin meatball ham hock shoulder capicola tri-tip sausage biltong cupim",
                    null,
                    CreateImBackButtons(numberOfButtons)
                )
            };

            return true;
        }

        private async Task<bool> Hero3Message()
        {
            string title = GetArg(1);

            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetHeroCardAttachment(
                    title,
                    null,
                    null,
                    null,
                    CreateImBackButtons(5)
                )
            };

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> Hero4Message()
        {
            string imgURL = GetArg(1);
            if (imgURL == null) imgURL = S_STANDARD_IMGURL;

            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetHeroCardAttachment(
                    null,
                    null,
                    null,
                    new string[] { imgURL  },
                    CreateInvokeButtons(5)
                )
            };
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> HeroRYOMessage()
        {
            string title = GetArg(1);
            string subTitle = GetArg(2);
            string content = GetArg(3);
            string imgURL = GetArg(4);
            int buttonCount = GetArgInt(5);

            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetHeroCardAttachment(
                    title,
                    subTitle,
                    content,
                    (imgURL == null ? null : new string[] { imgURL  }),
                    CreateImBackButtons(buttonCount)
                )
            };

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> ThumbRYOMessage()
        {
            string title = GetArg(1);
            string subTitle = GetArg(2);
            string content = GetArg(3);
            string imgURL = GetArg(4);
            int buttonCount = GetArgInt(5);

            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetThumbnailCardAttachment(
                    title,
                    subTitle,
                    content,
                    (imgURL == null ? null : new string[] { imgURL }),
                    CreateImBackButtons(buttonCount)
                )
            };

            return true;

        }

        /// <summary>
        /// This will display an Img in the Content section of the Attachment, instead of the Image section.
        /// </summary>
        private async Task<bool> ImgCardMessage()
        {

            string imgURL = GetArg(1);
            if (imgURL == null) imgURL = S_STANDARD_IMGURL;


            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetHeroCardAttachment(
                    "Card with image containing no width or height",
                    null,
                    "<img src='" + imgURL + "'/>",
                    null,
                    CreateImBackButtons(2)
                )
            };

            return true;
        }


        private async Task<bool> AnimationCardMessage()
        {
            //Not supported in Teams as of 4/2/2017
            m_replyMessage.Text = "Not currently supported in Teams";

            var animCard = new AnimationCard
            {
                Title = "Animation test",
                Subtitle = "Subtitle",
                Image = new ThumbnailUrl("https://docs.botframework.com/en-us/images/faq-overview/botframework_overview_july.png"),
                Media = new List<MediaUrl>
                {
                    new MediaUrl()
                    {
                        Url = "http://i.giphy.com/Ki55RUbOV5njy.gif"
                    }
                }
            };

            var attachments = new List<Attachment>();


            attachments.Add( new Attachment()
                {
                    ContentType = AnimationCard.ContentType,
                    Content = animCard
                }
            );


            m_replyMessage.Attachments = attachments;

            return true;
        }

        private async Task<bool> VideoCardMessage()
        {
            //Not supported in Teams as of 4/2/2017
            m_replyMessage.Text = "Not currently supported in Teams";

            var videoCard = new VideoCard
            {
                Title = "Big Buck Bunny",
                Subtitle = "by the Blender Institute",
                Text = "Big Buck Bunny (code-named Peach) is a short computer-animated comedy film by the Blender Institute, part of the Blender Foundation. Like the foundation's previous film Elephants Dream, the film was made using Blender, a free software application for animation made by the same foundation. It was released as an open-source film under Creative Commons License Attribution 3.0.",
                Image = new ThumbnailUrl
                {
                    Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c5/Big_buck_bunny_poster_big.jpg/220px-Big_buck_bunny_poster_big.jpg"
                },
                Media = new List<MediaUrl>
                {
                    new MediaUrl()
                    {
                        Url = "http://download.blender.org/peach/bigbuckbunny_movies/BigBuckBunny_320x180.mp4"
                    }
                },
                Buttons = new List<CardAction>
                {
                    new CardAction()
                    {
                        Title = "Learn More",
                        Type = ActionTypes.OpenUrl,
                        Value = "https://peach.blender.org/"
                    }
                }
            };

            var attachments = new List<Attachment>();

            attachments.Add(videoCard.ToAttachment());


            m_replyMessage.Attachments = attachments;

            return true;
        }

        private async Task<bool> AudioCardMessage()
        {
            //Not supported in Teams as of 4/2/2017
            m_replyMessage.Text = "Not currently supported in Teams";

            var audioCard = new AudioCard
            {
                Title = "I am your father",
                Subtitle = "Star Wars: Episode V - The Empire Strikes Back",
                Text = "The Empire Strikes Back (also known as Star Wars: Episode V – The Empire Strikes Back) is a 1980 American epic space opera film directed by Irvin Kershner. Leigh Brackett and Lawrence Kasdan wrote the screenplay, with George Lucas writing the film's story and serving as executive producer. The second installment in the original Star Wars trilogy, it was produced by Gary Kurtz for Lucasfilm Ltd. and stars Mark Hamill, Harrison Ford, Carrie Fisher, Billy Dee Williams, Anthony Daniels, David Prowse, Kenny Baker, Peter Mayhew and Frank Oz.",
                Image = new ThumbnailUrl
                {
                    Url = "https://upload.wikimedia.org/wikipedia/en/3/3c/SW_-_Empire_Strikes_Back.jpg"
                },
                Media = new List<MediaUrl>
                {
                    new MediaUrl()
                    {
                        Url = "http://www.wavlist.com/movies/004/father.wav"
                    }
                },
                Buttons = new List<CardAction>
                {
                    new CardAction()
                    {
                        Title = "Read More",
                        Type = ActionTypes.OpenUrl,
                        Value = "https://en.wikipedia.org/wiki/The_Empire_Strikes_Back"
                    }
                }
            };


            var attachments = new List<Attachment>();
            attachments.Add(audioCard.ToAttachment());
            m_replyMessage.Attachments = attachments;

            return true;
        }

        /// <summary>
        /// Carousel with 5 different cards
        /// </summary>
        private async Task<bool> Carousel1Message()
        {

            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetHeroCardAttachment(
                    null,
                    null,
                    null,
                    new string[] { S_STANDARD_IMGURL },
                    CreateImBackButtons(5)
                ),
                GetHeroCardAttachment(
                    "Subject Title Carousel 2",
                    null,
                    null,
                    null,
                    CreateImBackButtons(4)
                 ),
                 GetHeroCardAttachment(
                    "Subject Title Carousel 3",
                    "Subtitle or breadcrumb",
                    LoremIpsum(12,2),
                    null,
                    CreateInvokeButtons(3)
                ),
                GetHeroCardAttachment(
                    "Subject Title Carousel 4",
                    "Subtitle or breadcrumb",
                    LoremIpsum(8,2,2),
                    new string[] { S_STANDARD_IMGURL },
                    CreateImBackButtons(2)
                ),
                GetHeroCardAttachment(
                    "Subject Title Carousel 5",
                    null,
                    LoremIpsum(7,5),
                    null,
                    CreateImBackButtons(1)
                )                
           };
            m_replyMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;

            return true;
        }

        /// <summary>
        /// Carousel with 5 duplicate cards
        /// </summary>
        private async Task<bool> CarouselxMessage()
        {
            int numberOfCards = GetArgInt(1);
            if (numberOfCards == -1) numberOfCards = 5;

            var card = GetHeroCardAttachment(
                "Subject Title Carouselx",
                "Note: Teams currently supports a max of 5 cards",
                "Bacon ipsum dolor amet flank ground round chuck pork loin. Sirloin meatloaf boudin meatball ham hock shoulder capicola tri-tip sausage biltong cupim",
                new string[] { S_STANDARD_IMGURL },
                CreateImBackButtons(7)  // Teams only support 6 actions max. Send more.
             );

            var attachments = new List<Attachment>();

            for (var i = 0; i < numberOfCards; i++) // Teams only supports 5 attachments, sending more than that causes a Chat Service issue.
            {
                attachments.Add(card);
            }

            m_replyMessage.Attachments = attachments;
            m_replyMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;

            return true;
        }

        /// <summary>
        /// List with 5 different cards
        /// </summary>
        private async Task<bool> List1Message()
        {

            m_replyMessage.Attachments = new List<Attachment>()
            {
                GetHeroCardAttachment(
                    null,
                    null,
                    null,
                    new string[] { S_STANDARD_IMGURL },
                    CreateImBackButtons(5)
                ),
                GetHeroCardAttachment(
                    "Subject Title List 2",
                    null,
                    null,
                    null,
                    CreateImBackButtons(4)
                 ),
                 GetHeroCardAttachment(
                    "Subject Title List 3",
                    "Subtitle or breadcrumb",
                    LoremIpsum(12,2),
                    null,
                    CreateInvokeButtons(3)
                ),
                GetHeroCardAttachment(
                    "Subject Title List 4",
                    "Subtitle or breadcrumb",
                    LoremIpsum(8,2,2),
                    new string[] { S_STANDARD_IMGURL },
                    CreateImBackButtons(2)
                ),
                GetHeroCardAttachment(
                    "Subject Title List 5",
                    null,
                    LoremIpsum(7,5),
                    null,
                    CreateImBackButtons(1)
                )
           };
            m_replyMessage.AttachmentLayout = AttachmentLayoutTypes.List;

            return true;
        }


        private async Task<bool> ListxMessage()
        {
            int numberOfCards = GetArgInt(1);
            if (numberOfCards == -1) numberOfCards = 5;

            var card = GetHeroCardAttachment(
                "Subject Title Carouselx",
                "Note: Teams currently supports a max of 5 cards",
                "Bacon ipsum dolor amet flank ground round chuck pork loin. Sirloin meatloaf boudin meatball ham hock shoulder capicola tri-tip sausage biltong cupim",
                new string[] { S_STANDARD_IMGURL },
                CreateImBackButtons(7)  // Teams only support 6 actions max. Send more.
             );

            var attachments = new List<Attachment>();

            for (var i = 0; i < numberOfCards; i++) // Teams only supports 5 attachments, sending more than that causes a Chat Service issue.
            {
                attachments.Add(card);
            }

            m_replyMessage.Attachments = attachments;
            m_replyMessage.AttachmentLayout = AttachmentLayoutTypes.List;

            return true;
        }


        /// <summary>
        /// Sign-in Card type
        /// 
        /// NOTE: you can't use the signin action type - use openURL
        /// </summary>
        private async Task<bool> SignInMessage()
        {
            string openURL = GetArg(1);
            if (openURL == null) openURL = "https://www.bing.com";

            var card = new Attachment()
            {
                ContentType = SigninCard.ContentType,
                Content = new SigninCard()
                {
                    Text = "Sample Sign-in with OpenURL action (launch " + openURL + ")",
                    Buttons = new List<CardAction>()
                    {
                        new CardAction()
                        {
                            Title = "Sign In (signin type)",
                            Type = "signin",
                            Value = openURL
                        }
                    }
                }
            };
            m_replyMessage.Attachments = new List<Attachment> { card };

            return true;
        }

        /// <summary>
        /// Respond to Invoke button click
        /// </summary>
        private void InvokeResponse()
        {

            var text = "### Received Invoke action from button. ###\n\n";
            text += "Payload is: \n\n";

            //Get payload here:
            JObject payload = (JObject) m_sourceMessage.Value;
            text += payload.ToString();


            m_replyMessage.Text = text;
            m_dumpRequested = DUMPIN;


        }

        /// <summary>
        /// Respond to ImBack button click
        /// </summary>
        private async Task<bool> ImBackResponse()
        {

            var text = "### Received ImBack action from button. ###\n\n";
            text += "Message is: \n\n";

            //Get payload here:
            text += m_sourceMessage.Text;


            m_replyMessage.Text = text;
            m_dumpRequested = DUMPIN;

            return true;
        }

        /// <summary>
        /// Test XML formatting
        /// </summary>
        private async Task<bool> FormatXMLMessage()
        {
            var text = GetArg(1);
            if (text == null)
            {
                var tmp = new string[]
                {
                        "<h1>H1</h1>",
                        "<h2>H2</h2>",
                        "<h3>H3</h3>",
                        "<h4>H4</h4>",
                        "<h5>H5 (max)</h5>",
                        "<b>Bold</b>",
                        "<i>Italic</i>",
                        "<strike>Strike</strike>",
                        "<pre>code() using pre</pre>",
                        "<hr />",
                        "emoji - " + EmojiToSurrogatePair(0x1F37D),
                        "<ul><li>Unordered item 1</li><li>Unordered item 2</li><li>Unordered item 3</li></ul>",
                        "<ol><li>Ordered item 1</li><li>Ordered item 2</li><li>Ordered item 3</li></oll>",
 
                        "<a href='https://bing.com'>Link</a>",
                        "<img src='http://aka.ms/Fo983c' alt='Test image' />"
                };

                text = string.Join("<br />", tmp);
            }

            m_replyMessage.Text = text;
            m_replyMessage.TextFormat = TextFormatTypes.Xml;

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> FormatMDMessage()
        {
            var text = GetArg(1);
            if (text == null)
            {
                var tmp = new string[]
                {
                    "# H1",
                    "## H2",
                    "### H3",
                    "#### H4",
                    "##### H5 (max)",
                    "**Bold**",
                    "*Italic*",
                    "~~Strike~~",
                    "`code()`",
                    "> Block",
                    "---",
                    "emoji - " + EmojiToSurrogatePair(0x1f37a), //\uD83C\uDF20 ",
                    "This is a Table:\n\n|Table Col 1|Col2|Column 3|\n|---|---|---|\n| R1C1 | Row 1 Column 2 | Row 1 Col 3 |\n|R2C1|R2C2|R2C3|\n\n",
                    "* Unordered item 1\n* Unordered item 2\n* Unordered item 3\n",
                    "1. Ordered item 1\n2. Ordered item 2\n3. Ordered item 3\n",

                    "[Link](https://bing.com)",
                    "![Alt Text](http://aka.ms/Fo983c)"
                };

                text = string.Join("\n\n", tmp);
            }

            m_replyMessage.Text = text;
            m_replyMessage.TextFormat = TextFormatTypes.Markdown;

            return true;
        }


        /// <summary>
        /// Simple echo back with Markdown
        /// </summary>
        private async Task<bool> EchoMessage()
        {
     
            //Remove "echo" and take everything else
            var text = StripCommandFromMessage();

            m_replyMessage.Text = text;
            m_replyMessage.TextFormat = TextFormatTypes.Markdown;

            return true;
        }


        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> ThumbnailMessage()
        {

            var card = GetThumbnailCardAttachment(
                "Homegrown Thumbnail Card",
                "Sandwiches and salads",
                "104 Lake St, Kirkland, WA 98033/n/n(425) 123-4567",
                new string[] { S_THUMB_IMGURL },
                CreateImBackButtons(2)
            );

            m_replyMessage.Attachments = new List<Attachment> { card };

            return true;

        }

        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> ThumbnailListMessage()
        {
            int numberOfCards = GetArgInt(1);
            if (numberOfCards == -1) numberOfCards = 5;

            var card = GetThumbnailCardAttachment(
                "Homegrown Thumbnail Card",
                "Sandwiches and salads",
                "104 Lake St, Kirkland, WA 98033/n/n(425) 123-4567",
                new string[] { S_THUMB_IMGURL },
                CreateImBackButtons(3)
            );
            
            var attachments = new List<Attachment>();

            for (var i = 0; i < numberOfCards; i++) // Teams only supports 5 attachments, sending more than that causes a Chat Service issue.
            {
                attachments.Add(card);
            }

            m_replyMessage.Attachments = attachments;
            m_replyMessage.AttachmentLayout = AttachmentLayoutTypes.List;

            return true;
        }


        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> GetAttachMessage()
        {
            //Attachments always have text, so we need at least 2 attachments
            if (m_sourceMessage.Attachments == null || m_sourceMessage.Attachments.Count < 2)
            {
                m_replyMessage.Text = "Please paste an inline image/gif in your message";
                return true;
            }

            //Assume (which is risky) first attachment = image
            var attachment = m_sourceMessage.Attachments.First();
            using (HttpClient httpClient = new HttpClient())
            {
                // Skype & MS Teams attachment URLs are secured by a JwtToken, so we need to pass the token from our bot.
                if (new Uri(attachment.ContentUrl).Host.EndsWith("skype.com"))
                {
                    var token = await new MicrosoftAppCredentials().GetTokenAsync();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var responseMessage = await httpClient.GetAsync(attachment.ContentUrl);

                var contentLengthBytes = responseMessage.Content.Headers.ContentLength;

                m_replyMessage.Text = "I received a file of type: " + responseMessage.Content.Headers.ContentType + ", size: " + contentLengthBytes;
                await m_dialogContext.PostAsync(m_replyMessage);

                Activity dumpReply = DumpMessage(m_sourceMessage, m_replyMessage);
                if (dumpReply != null) await m_dialogContext.PostAsync(dumpReply);

            }
            return false;

        }



        private async Task<bool> ChannelsTest()
        {

            ConnectorClient connector = new ConnectorClient(new Uri(m_sourceMessage.ServiceUrl));

            ConversationList channels = connector.GetTeamsConnectorClient().Teams.FetchChannelList(m_sourceMessage.GetChannelData<TeamsChannelData>().Team.Id);

            m_replyMessage.Text = "There are " + channels.Conversations.Count + " channels: \n\n\n";
            foreach (ChannelInfo c in channels.Conversations)
            {
                m_replyMessage.Text += "**ID:** " + c.Id;
                m_replyMessage.Text += "<br>**Name:** " + c.Name + "<br>";
            }
            return true;
        }

        /// <summary>
        /// This routine is leveraging the fact that my inbound message has the user and bot identities, as well as the tenantID.
        /// 
        /// Note that creating a new conversation is a multi-step process:  
        ///     1) CreateOrGetDirectConversation with the appropriate conversation parameters will return the conversationID for the user
        ///     2) SendMessage to the conversationID to send the actual text.
        /// </summary>
        private async Task<bool> Create11Conversation()
        {
            //Message for main loop
            m_replyMessage.Text = "Should have just sent to 1:1";

            //Create new message
            ConnectorClient connector = new ConnectorClient(new Uri(m_sourceMessage.ServiceUrl));
            var response = connector.Conversations.CreateOrGetDirectConversation(m_sourceMessage.Recipient, m_sourceMessage.From, m_sourceMessage.GetTenantId());

            Activity newActivity = new Activity()
            {
                Text = "Hello, this is a 1:1 message created by me - " + m_sourceMessage.Recipient.Name,
                Type = ActivityTypes.Message,
                Conversation = new ConversationAccount
                {
                    Id = response.Id
                },
            };

            await connector.Conversations.SendToConversationAsync(newActivity, response.Id);

            Activity dumpReply = DumpMessage(m_sourceMessage, newActivity);
            if (dumpReply != null) await m_dialogContext.PostAsync(dumpReply);

            return false;
        }

        /// <summary>
        /// To test CreateConversationAsync set the Conversation to null, which triggers the creation of a new one in the MessageController post flow
        /// </summary>
        private async Task<bool> CreateConversation()
        {

            //Check to validate this is in group context.
            if (m_sourceMessage.Conversation.IsGroup != true)
            {
                m_replyMessage.Text = "CreateConversation only works in channel context at this time";
                return true;
            }

            //Message for main loop
            m_replyMessage.Text = "Creating new Conversation thread";

            //Create new message:
            Activity newActivity = new Activity()
            {
                Type = ActivityTypes.Message,
            };

            newActivity.Text = "This is a new Conversation created with CreateConversationAsync().";
            newActivity.Text += "<br/><br/> **ChannelID:** " + m_sourceMessage.ChannelId;
            newActivity.Text += "<br/>**ConversationID (in):** " + m_sourceMessage.Conversation.Id;

            ConversationParameters conversationParams = new ConversationParameters(
                isGroup: true,
                bot: null,
                members: null,
                topicName: "New Conversation",
                activity: newActivity,
                channelData: m_sourceMessage.ChannelData
            );

            await getConnector().Conversations.CreateConversationAsync(conversationParams);

            Activity dumpReply = DumpMessage(m_sourceMessage, newActivity);
            if (dumpReply != null) await m_dialogContext.PostAsync(dumpReply);

            return false;

        }

        /// <summary>
        /// Retrieve and display all Team Members, leveraging the GetConversationMembers function from BotFramework.  Note that this only has relevance in a Group context.
        /// </summary>
        private async Task<bool> MembersTest() 
        {

            //Check to validate this is in group context.
            if (m_sourceMessage.Conversation.IsGroup != true)
            {
                m_replyMessage.Text = "GetConversationMembers only work in channel context at this time";
                return true;
            }

            var localReply = m_replyMessage;
            m_replyMessage = null;

            ConnectorClient c = new ConnectorClient(new Uri(m_sourceMessage.ServiceUrl));
            var t = m_sourceMessage.GetTenantId();

            var members = await c.Conversations.GetTeamsConversationMembersAsync(m_sourceMessage.Conversation.Id, t);

            localReply.Text = "These are the member userids returned by the GetConversationMembers() function.\n\n";

            var sb = new System.Text.StringBuilder();
            foreach (var member in members)
            {
                sb.AppendFormat(
                       "**Teams Id:** {0}<br>**GivenName:** {1}<br>**Surname:** {2}<br>**Email:** {3}<br>**UserPrincipalName:** {4}<br>**AADObjectId:** {5}<br><br>",
                       member.Id, member.GivenName, member.Surname, member.Email, member.UserPrincipalName, member.ObjectId );

                sb.AppendLine();
            }
            localReply.Text += sb;

            await m_dialogContext.PostAsync(localReply);

            Activity dumpReply = DumpMessage(m_sourceMessage, localReply);
            if (dumpReply != null) await m_dialogContext.PostAsync(dumpReply);

            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> MentionsTest()
        {
            Mention[]  m = m_sourceMessage.GetMentions();

            var text = "You mentioned " + m.Length + " entities";
            for (int i = 0; i < m.Length; i++)
            {
                text += "<br />Text: " + m[i].Text + ", name: " + m[i].Mentioned.Name;
            }

            m_replyMessage.Text = text;
            m_replyMessage.TextFormat = TextFormatTypes.Markdown;

            return true;

        }

        /// <summary>
        /// 
        /// </summary>
        private async Task<bool> MentionUser()
        {
            Mention[] m = m_sourceMessage.GetMentions();

            string text = null;
            Mention mentionedUser = null;
            for (int i = 0; i < m.Length; i++)
            {
                if (m[i].Mentioned.Id != m_sourceMessage.Recipient.Id)
                {
                    //get the first non-bot user
                    mentionedUser = m[i];
                    break;
                }

            }
            if (mentionedUser != null)
            {
                text = "Here is a mention:  Hello " + mentionedUser.Text;
                m_replyMessage.Entities.Add((Entity)mentionedUser);
            }
            else
            {
                text = "No **users** mentioned";
            }

            m_replyMessage.Text = text;
            m_replyMessage.TextFormat = TextFormatTypes.Markdown;

            return true;

        }

        private async Task<bool> ConnectorCardTest()
        {

            var section = new O365ConnectorCardSection(
             "This is the **section's title** property",
             "This is the section's text property. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
             "This is the section's activityTitle property",
             "This is the section's activitySubtitle property",
             "This is the section's activityText property.",
             "http://connectorsdemo.azurewebsites.net/images/MSC12_Oscar_002.jpg",
             new List<O365ConnectorCardFact>()
             {
                            new O365ConnectorCardFact("This is a fact name", "This is a fact value"),
                            new O365ConnectorCardFact("This is a fact name", "This is a fact value"),
                            new O365ConnectorCardFact("This is a fact name", "This is a fact value")
             },
             new List<O365ConnectorCardImage>()
             {
                            new O365ConnectorCardImage("http://connectorsdemo.azurewebsites.net/images/MicrosoftSurface_024_Cafe_OH-06315_VS_R1c.jpg"),
                            new O365ConnectorCardImage("http://connectorsdemo.azurewebsites.net/images/WIN12_Scene_01.jpg"),
                            new O365ConnectorCardImage("http://connectorsdemo.azurewebsites.net/images/WIN12_Anthony_02.jpg")
             },
             new List<O365ConnectorCardActionBase>()
             {
                            new O365ConnectorCardViewAction()
                            {
                                Type = O365ConnectorCardViewAction.ContentType,
                                Name = "View",
                                Target = new List<string>() { "http://microsoft.com" }
                            },
                            new O365ConnectorCardViewAction()
                            {
                                Type = O365ConnectorCardViewAction.ContentType,
                                Name = "View",
                                Target = new List<string>() { "http://microsoft.com" }
                            }
             });



            var card = new O365ConnectorCard(
                "This is the card title property",
                "This is the card's text property. Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
                "This is the summary property",
                "E81123",
                new List<O365ConnectorCardSection>() { section }
            );

            m_replyMessage.Attachments = new List<Attachment>()
            {
                new Attachment
                {
                    Content = card,
                    ContentType = O365ConnectorCard.ContentType
                }
                
            };

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task<bool> UpdateMessage()
        {

            m_replyMessage = null;

            ConnectorClient connector = getConnector();

            string message = $"You sent {m_sourceMessage.Text} which was {m_sourceMessage.Text.Length} characters";

            Activity reply = m_sourceMessage.CreateReply(message);

            var msgToUpdate = await connector.Conversations.ReplyToActivityAsync(reply);

            Activity updatedReply = m_sourceMessage.CreateReply("This is an updated message.  Previous message was: <br/>" + message);

            await connector.Conversations.UpdateActivityAsync(reply.Conversation.Id, msgToUpdate.Id, updatedReply);

            Activity dumpReply = DumpMessage(m_sourceMessage, updatedReply);
            if (dumpReply != null) await m_dialogContext.PostAsync(dumpReply);

            return false;

        }

        private async Task<bool> SuggestedActionTest()
        {
            m_replyMessage.Text = "I have colors in mind, but need your help to choose the best one.";

            m_replyMessage.SuggestedActions = new SuggestedActions()
            {
                Actions = new List<CardAction>()
                {
                    new CardAction(){ Title = "Blue", Type=ActionTypes.ImBack, Value="Blue" },
                    new CardAction(){ Title = "Red", Type=ActionTypes.ImBack, Value="Red" },
                    new CardAction(){ Title = "Green", Type=ActionTypes.ImBack, Value="Green" }
                }
            };

            return true;
        }


        private async Task<bool> InputHintsTest()
        {
            m_replyMessage.Text = "This is the text that will be displayed.";
            m_replyMessage.Speak = "This is the text that will be spoken.";
            m_replyMessage.InputHint = InputHints.AcceptingInput;


            return true;
        }

        /// <summary>
        /// Builds and returns a <see cref="HeroCard"/> attachment using the supplied info
        /// </summary>
        /// <param name="title">Title of the card</param>
        /// <param name="subTitle">Subtitle of the card</param>
        /// <param name="text">Text of the card</param>
        /// <param name="images">Images in the card</param>
        /// <param name="buttons">Buttons in the card</param>
        /// <returns>Card attachment</returns>
        private static Attachment GetHeroCardAttachment(string title, string subTitle, string text, string[] images, CardAction[] buttons)
        {
            var heroCard = new HeroCard()
            {
                Title = title,
                Subtitle = subTitle,
                Text = text,
                Images = new List<CardImage>(),
                Buttons = new List<CardAction>(),
            };

            // Set images
            if (images != null)
            {
                foreach (var img in images)
                {
                    heroCard.Images.Add(new CardImage()
                    {
                        Url = img,
                        Alt = img,
                    });
                }
            }

            // Set buttons
            if (buttons != null)
            {
                heroCard.Buttons = buttons;
            }

            return new Attachment()
            {
                ContentType = HeroCard.ContentType,
                Content = heroCard,
            };
        }


        /// <summary>
        /// Builds and returns a <see cref="ThumbnailCard"/> attachment using the supplied info
        /// </summary>
        /// <param name="title">Title of the card</param>
        /// <param name="subTitle">Subtitle of the card</param>
        /// <param name="text">Text of the card</param>
        /// <param name="images">Images in the card</param>
        /// <param name="buttons">Buttons in the card</param>
        /// <returns>Card attachment</returns>
        private static Attachment GetThumbnailCardAttachment(string title, string subTitle, string text, string[] images, CardAction[] buttons)
        {
            var heroCard = new ThumbnailCard()
            {
                Title = title,
                Subtitle = subTitle,
                Text = text,
                Images = new List<CardImage>(),
                Buttons = new List<CardAction>(),
            };

            // Set images
            if (images != null)
            {
                foreach (var img in images)
                {
                    string altText = null;
                    if (img.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        altText = img;
                    }
                    else
                    {
                        altText = "The alt text for an image blob";
                    }

                    heroCard.Images.Add(new CardImage()
                    {
                        Url = img,
                        Alt = altText,
                    });
                }
            }

            // Set buttons
            if (buttons != null)
            {
                heroCard.Buttons = buttons;
            }

            return new Attachment()
            {
                ContentType = ThumbnailCard.ContentType,
                Content = heroCard,
            };
        }

        /// <summary>
        /// Generates random paragraphs.  Numbers are multiplicative (e.g. word per sentence, sentences per line.)
        /// </summary>
        /// <param name="numWords"></param>
        /// <param name="numSentences"></param>
        /// <param name="numLines"></param>
        /// <returns></returns>
        private static string LoremIpsum(int numWords, int numSentences = 1, int numLines = 1)
        {
            var words = new[] { "lorem", "ipsum", "dolor", "sit", "amet", "capicola", "meatball", "elit", "ham", "pork", "nonummy", "cube", "tri-tip", "pepperoni", "ut", "sausage", "chuck", "bacon", "meatloaf", "flank" };

            var rand = new Random();
            bool capitalize = true;

            var sb = new System.Text.StringBuilder();
            for (int p = 0; p < numLines; p++)
            {
                for (int s = 0; s < numSentences; s++)
                {
                    for (int w = 0; w < numWords; w++)
                    {
                        if (w > 0) { sb.Append(" "); }
                        string nextWord = (words[rand.Next(words.Length)]);
                        if (capitalize)
                        {
                            nextWord = char.ToUpper(nextWord[0]) + nextWord.Substring(1);
                            capitalize = false;
                        }
                        sb.Append(nextWord);                   
                        //sb.Append(words[rand.Next(words.Length)]);
                    }
                    sb.Append(". ");
                    capitalize = true;
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }



}

