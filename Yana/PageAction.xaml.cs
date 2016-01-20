using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Newtonsoft.Json;
using Flurl.Http;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Networking.Connectivity;

namespace Yana
{

    public sealed partial class PageAction : Page
    {

        Windows.Storage.ApplicationDataContainer localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;

        private bool isVisible = false;

        List<List<string>> commandesHttp = new List<List<string>>{
                new List<string>{},
                new List<string>{}
            };

        List<string> commandesSocket = new List<string> { };

        string serveurAdresse;

        bool tts;

        ObservableCollection<Message> allmessages;

        private StreamSocket socket;

        bool m_eventHandled = false;
        public event TypedEventHandler<PageAction, DataReceivedEventArgs> DataReceived;


        public PageAction()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;

            allmessages = new ObservableCollection<Message>();

            this.myChat.ItemsSource = allmessages;
        
            }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

        }

       
        async Task<string> getCommand()
        {
            string url = String.Format("http://{0}/yana-server/action.php?action=GET_SPEECH_COMMAND&token={1}",serveurAdresse,localSettings.Values["savedToken"].ToString());
            var responseString = await url.GetStringAsync();
            return responseString;
        }

        private async void isLoaded(object sender, RoutedEventArgs e)
        {         
            tts = (bool)localSettings.Values["tts"];

            //Verification du réseau : data ou wifi
            var connectionProfile = NetworkInformation.GetInternetConnectionProfile();
            if (connectionProfile != null && connectionProfile.IsWlanConnectionProfile)
            {
                // Wifi Connect
                serveurAdresse = localSettings.Values["savedServerInt"].ToString();
            }
            else
            {
                //Data Connect
                serveurAdresse = localSettings.Values["savedServerExt"].ToString();
            }
            serveur.Text = "Serveur : " + serveurAdresse + "/yana-server";

            //Recuperation des commandes
            string json = await getCommand();

            Newtonsoft.Json.Linq.JObject jsonNoData = (Newtonsoft.Json.Linq.JObject)JsonConvert.DeserializeObject(json);
            foreach (var command in jsonNoData["commands"])
            {
                if (command["command"] != null && command["url"] != null)
                {
                    commandesHttp[0].Add(command["command"].ToString());
                    commandesHttp[1].Add(command["url"].ToString());
                }
                else commandesSocket.Add(command["command"].ToString());
            }

            List<string> allCommandes = commandesHttp[0];
            foreach (var command in commandesSocket)
            {
                allCommandes.Add(command);
            }

            ListCommande.ItemsSource =allCommandes;

            //Connection socket
            try
            {
                StartSockets(serveurAdresse, localSettings.Values["savedPort"].ToString(), String.Format("{{\"action\":\"CLIENT_INFOS\",\"version\":\"2\",\"type\":\"speak\",\"location\":\"mobile\",\"token\":\"{0}\"}}<EOF>", localSettings.Values["savedToken"].ToString()));
                await connectSocket(serveurAdresse, localSettings.Values["savedPort"].ToString(), String.Format("{{\"action\":\"CLIENT_INFOS\",\"version\":\"2\",\"type\":\"listen\",\"location\":\"mobile\",\"token\":\"{0}\"}}<EOF>", localSettings.Values["savedToken"].ToString()));
                
            }
            catch
            {
                allmessages.Add(new Message { TextMessage = "Impossible de joindre le serveur socket", Time = DateTime.Now.ToString(), Status = "Sent", tofrom = false });
                myChat.UpdateLayout();
                myChat.ScrollIntoView(allmessages.Last());
            }
        }

        //Affichage liste des commandes
        private void commandes_Click(object sender, RoutedEventArgs e)
        {
            if (!isVisible)
            {
                ListCommande.Visibility = Visibility.Visible;
                send.Visibility = Visibility.Visible;
                cancel.Visibility = Visibility.Visible;
                isVisible = true;
            }
            else
            {
                ListCommande.Visibility = Visibility.Collapsed;
                send.Visibility = Visibility.Collapsed;
                cancel.Visibility = Visibility.Collapsed;
                isVisible = false;
            }
        }

        //Bouton de validation de la commande
        private async void send_Click(object sender, RoutedEventArgs e)
        {
            allmessages.Add(new Message { TextMessage = commandesHttp[0][ListCommande.SelectedIndex], Time = DateTime.Now.ToString(), Status = "Sent", tofrom = true });
            myChat.UpdateLayout();
            myChat.ScrollIntoView(allmessages.Last());

            ListCommande.Visibility = Visibility.Collapsed;
            send.Visibility = Visibility.Collapsed;
            cancel.Visibility = Visibility.Collapsed;
            isVisible = false;

            string reponse="";

            try
            {   
                // Verification si commande est http ou socket
                if (ListCommande.SelectedIndex < commandesHttp[1].Count)
                {
                    //http commande
                    await getReponseHttp(commandesHttp[1][ListCommande.SelectedIndex]);
                }
                else
                {
                    //Socket commande
                   getReponseSocket(ListCommande.SelectedItem.ToString());
                }
                
            }
            catch
            {
                reponse = "Une erreur est survenue";
                allmessages.Add(new Message { TextMessage = reponse, Time = DateTime.Now.ToString(), Status = "Sent", tofrom = false });
                if(tts) parle(reponse);
            }            
            myChat.UpdateLayout();
            myChat.ScrollIntoView(allmessages.Last());

        }

        //Masque la liste des commandes
        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            ListCommande.Visibility = Visibility.Collapsed;
            send.Visibility = Visibility.Collapsed;
            cancel.Visibility = Visibility.Collapsed;
            isVisible = false;
        }

        //Envoi commande http + affichage reponse
        private async Task<string> getReponseHttp(string url)
        {
            string retour = "Pas de reponse...";

            string sendUrl = url + String.Format("&token={0}", localSettings.Values["savedToken"].ToString());

            var json = await sendUrl.GetStringAsync();


            Newtonsoft.Json.Linq.JObject jsonNoData = (Newtonsoft.Json.Linq.JObject)JsonConvert.DeserializeObject(json);

            foreach (var reponse in jsonNoData["responses"])
            {
                retour = reponse["sentence"].ToString();
                allmessages.Add(new Message { TextMessage = retour, Time = DateTime.Now.ToString(), Status = "Sent", tofrom = false });
                myChat.UpdateLayout();
                myChat.ScrollIntoView(allmessages.Last());
                if (tts) parle(retour);
            }

            return retour;
        }

        //Envoi commande socket
        private async void getReponseSocket(string commande)
        {
            string retour = "Pas de reponse...";

            await Send(String.Format("{{\"action\":\"CATCH_COMMAND\",\"command\":\"{0}\",\"confidence\":\"0.9\",\"text\":\"\"}}<EOF>",commande));

            retour = await read();

        }

        //Connection socket pour client listen
        public async Task connectSocket(string host, string port, string message)
        {
            HostName hostName;

            using (socket = new StreamSocket())
            {
                hostName = new HostName(host);

                socket.Control.NoDelay = false;

                try
                {
                    // Connect to the server
                    await socket.ConnectAsync(hostName, localSettings.Values["savedPort"].ToString());
                    // Send the message
                    await this.Send(message);
                    // Read response
                    await this.read();
                }
                catch (Exception exception)
                {
                    switch (SocketError.GetStatus(exception.HResult))
                    {
                        case SocketErrorStatus.HostNotFound:
                            // Handle HostNotFound Error
                            throw;
                        default:
                            // If this is an unknown status it means that the error is fatal and retry will likely fail.
                            throw;
                    }
                }
            }
        }

        //Envoi de la commande socket
        public async Task Send(string message)
        {
            DataWriter writer;

            // Create the data writer object backed by the in-memory stream. 
            using (writer = new DataWriter(socket.OutputStream))
            {
                // Set the Unicode character encoding for the output stream
                writer.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                // Specify the byte order of a stream.
                writer.ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian;

                // Gets the size of UTF-8 string.
                writer.MeasureString(message);
                // Write a string value to the output stream.
                writer.WriteString(message);

                // Send the contents of the writer to the backing stream.
                try
                {
                    await writer.StoreAsync();
                }
                catch (Exception exception)
                {
                    switch (SocketError.GetStatus(exception.HResult))
                    {
                        case SocketErrorStatus.HostNotFound:
                            // Handle HostNotFound Error
                            throw;
                        default:
                            // If this is an unknown status it means that the error is fatal and retry will likely fail.
                            throw;
                    }
                }

                await writer.FlushAsync();
                // In order to prolong the lifetime of the stream, detach it from the DataWriter
                writer.DetachStream();
            }

        }

        //Ne sert a priori a rien mais si je le mets pas Exception sur lenvoi de commande via le client Listen
        public async Task<String> read()
        {
            DataReader reader;
            StringBuilder strBuilder;

            using (reader = new DataReader(socket.InputStream))
            {
                strBuilder = new StringBuilder();

                // Set the DataReader to only wait for available data (so that we don't have to know the data size)
                reader.InputStreamOptions = Windows.Storage.Streams.InputStreamOptions.Partial;
                // The encoding and byte order need to match the settings of the writer we previously used.
                reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                reader.ByteOrder = Windows.Storage.Streams.ByteOrder.LittleEndian;

                // Send the contents of the writer to the backing stream. 
                // Get the size of the buffer that has not been read.
                await reader.LoadAsync(256);

                // Keep reading until we consume the complete stream.
                while (reader.UnconsumedBufferLength > 0)
                {
                    strBuilder.Append(reader.ReadString(reader.UnconsumedBufferLength));
                    await reader.LoadAsync(256);
                }

                reader.DetachStream();
                return strBuilder.ToString();
            }
        }

        //Lancement reconnaissance vocale
        private async void micro_Click(object sender, RoutedEventArgs e)
        {
            var speechRecognizer = new Windows.Media.SpeechRecognition.SpeechRecognizer();

            string[] responses = commandesHttp[0].ToArray();

            //Ne compare que avec des commandes vocale connue
            var listConstraint = new Windows.Media.SpeechRecognition.SpeechRecognitionListConstraint(responses, "commandeHttp");

            speechRecognizer.UIOptions.ExampleText = @"Ex. 'Yana comment vas tu ?'";
            speechRecognizer.Constraints.Add(listConstraint);

            await speechRecognizer.CompileConstraintsAsync();

            Windows.Media.SpeechRecognition.SpeechRecognitionResult speechRecognitionResult = await speechRecognizer.RecognizeWithUIAsync();

            allmessages.Add(new Message { TextMessage = speechRecognitionResult.Text, Time = DateTime.Now.ToString(), Status = "Sent", tofrom = true });

            var index = Array.FindIndex(responses, row => row.Contains(speechRecognitionResult.Text));
            string reponse = "";

            //Verification si commande http ou socket
            try
            {
                if (index < commandesHttp[1].Count)
                {
                    reponse = await getReponseHttp(commandesHttp[1][index]);
                }
                else
                {
                    getReponseSocket(speechRecognitionResult.Text);
                }

            }
            catch
            {
                reponse = "Une erreur est survenue";
            }

        }

        //Affichage de la page de configuration
        private void config_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(configPage));
        }

        //Fonction de TTS
        private async void parle(string phrase)
        {
            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            SpeechSynthesisStream synthesisStream =
            await synthesizer.SynthesizeTextToStreamAsync(phrase);

            if (synthesisStream != null)
            {
                this.SpeechMedia.AutoPlay = true;
                this.SpeechMedia.SetSource(synthesisStream, synthesisStream.ContentType);
                this.SpeechMedia.Play();
            }
        }

        //Connection socket pour client Speak
        private async void StartSockets(string serveur, string port, string message)
        {
            if (!m_eventHandled){
                //Affichage des actions envoye par le serveur
                DataReceived += (s, args) =>  allmessages.Add(new Message { TextMessage = args.DataCount, Time = DateTime.Now.ToString(), Status = "Sent", tofrom = false }); 
            }
            m_eventHandled = true;

            DoSocketTestAsync(serveur, port, message);
        }

        //Connection socket pour client Speak
        private async Task DoSocketTestAsync(string serveur, string port, string message)
        {
            var _socket = new StreamSocket();
            await _socket.ConnectAsync(new HostName(serveur), port);
            var writer = new DataWriter(_socket.OutputStream);
            writer.WriteString(message);
            await writer.StoreAsync();

            ReadSocketAsync(_socket);
        }

        //Tache async qui ecoute en permanance le serveur
        private async void ReadSocketAsync(StreamSocket _socket)
        {
            while (true)
            {
                const uint size = 2048;
                IBuffer buffer = new Windows.Storage.Streams.Buffer(size);
                buffer = await _socket.InputStream.ReadAsync(buffer, size,
                 InputStreamOptions.Partial);
                var handler = DataReceived;
                if (handler != null && buffer.Length > 0)
                {
                    byte[] Array = buffer.ToArray();
                    string reponse = Encoding.UTF8.GetString(Array, 0, Array.Length);
                    if (reponse != "<EOF>")
                    {
                        string[] separator = new string[] { "<EOF>" };
                        string[] phrases = reponse.Split(separator, StringSplitOptions.None);

                        foreach (string phrase in phrases)
                        {
                            if (phrase != "")
                            {
                                Newtonsoft.Json.Linq.JObject jsonNoData = (Newtonsoft.Json.Linq.JObject)JsonConvert.DeserializeObject(phrase);

                                if (jsonNoData["action"].ToString() == "talk")
                                {
                                    reponse = jsonNoData["message"].ToString();
                                }
                                else if (jsonNoData["action"].ToString() == "execute")
                                {
                                    reponse = "Je ne peux pas faire cela...";
                                }
                                else if (jsonNoData["action"].ToString() == "sound")
                                {
                                    reponse = "Je ne peux pas faire cela...";
                                }   
                                handler(this, new DataReceivedEventArgs(reponse));
                                if (tts) parle(reponse);
                            }
                            
                        }
                        
                    }
                        

                         
                    if (allmessages.Count() != 0)
                    {
                        myChat.UpdateLayout();
                        myChat.ScrollIntoView(allmessages.Last());

                    }
                }

            }
        }

    }
}
