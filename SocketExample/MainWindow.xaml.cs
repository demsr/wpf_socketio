using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SocketIOClient;
using System.Windows.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace SocketExample
{


    /// <summary>
    /// Nachrichtenklasse
    /// Erbt von INotifyPropertyChanged um Änderungen an der Nachicht in der Oberfläche zu aktualisieren
    /// </summary>
    public class Message: INotifyPropertyChanged
    {
        /* Erforderlich für INotifyPropertyChanged */
        public event PropertyChangedEventHandler PropertyChanged;


        /* Eindeutige Nachrichten-ID */
        private string uid;
        public string UID { get { return uid; } set { uid = value; OnPropertyChanged(); } }
        /* Server-Nachrichtenempfang */
        private bool received;
        public bool Received { get { return received; } set { received = value; OnPropertyChanged();   } }


        private bool read;
        public bool Read { get { return read; } set { read = value; OnPropertyChanged(); } }

        /* Nachrichtentext */
        private string text;
        public string Text { get { return text; } set { text = value; } }
        /* Sender */
        private string sender;
        public string Sender { get { return sender; } set { sender = value; } }


        public Message() { }

        public Message(string MessageText) {

            this.uid = Guid.NewGuid().ToString("D");
            this.text = MessageText;

            this.sender = "Client";
        }

        

        public Message(string UID, string Text, string Sender)
        {

            this.uid = UID;
            this.text = Text;
            this.sender = Sender;
        }



        /* Muss immer aufgerufen werden sofern etwas geändert wird was in der Oberfläche reflektiert werden soll */
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>
    /// Nachricht-Gelesen Klasse
    /// Der Server schickt für jede Nachricht die UID zurück die er empfangen hat
    /// </summary>
    public class MessageRecipt
    {
        private string uid;
        public string UID { get { return uid; } }

        public MessageRecipt(string UID)
        {
            this.uid = UID;
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /* ObservableCollection benötigen keinen Aufruf des OnPropertyChanged Events */
        private ObservableCollection<Message> messages;
        public ObservableCollection<Message> Messages { get { return messages; } set { messages = value; } }

        /* Enthält den Text aus der Eingabe Textbox, bidirektionale Bindung an die Oberfläche */
        private string messageDraft;
        public string MessageDraft { get { return messageDraft; } set { messageDraft = value; OnPropertyChanged(); } }


        /* Die SocketIO Instanz, muss manuell verbunden werden */
        private SocketIO socket = new SocketIO("http://localhost:4000/", new SocketIOOptions { Path = "/ws" }); 

        public MainWindow()
        {
            InitializeComponent();


            /* verbindet Oberflächenbindungen mit unserem Code */
            this.DataContext = this;

            Messages = new ObservableCollection<Message>();
    
            /* Eventlistener für SocketIO */
            socket.On("pong", response =>
            {
                /* Nachdem wir mit den Server verbindung aufgebaut haben
                 * senden wir eine "Ping" NAchricht und erwarten hier eine 
                 * "Pong" Antwort um die Verbindung zu testen
                 */
                System.Diagnostics.Debug.WriteLine("received pong from server");
                System.Diagnostics.Debug.WriteLine(response);
            });

            
            socket.On("message", response =>
            {

                System.Diagnostics.Debug.WriteLine("received message from server");
                System.Diagnostics.Debug.WriteLine(response);
                Message m = response.GetValue<Message>();

               

                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    Messages.Add(m);
                }));

            });

            socket.On("read", response =>
            {

                System.Diagnostics.Debug.WriteLine("received read from server");

                /* Die Antwort in unsere "ReadReceipt" Klasse umwandeln */
                MessageRecipt r = response.GetValue<MessageRecipt>();
                System.Diagnostics.Debug.WriteLine("received read from server {0}", r.UID);

                /*
                    Sobald uns der Server eine "Gelesen" Bestätigung schickt suchen wir in unseren
                    Nachrichten nach der Nachricht mit der UID und setzen den "gelesen" Wert auf WAHR
                 */
                Message m = Messages.Single(x => x.UID == r.UID);


                if (m != null)
                {
                    m.Read = true;
                    int i = Messages.IndexOf(m);


                    System.Diagnostics.Debug.WriteLine("update message");

                    /* Oberfläche und SocketEvent laufen auf unterschiedlichen Threads, deshalb müssen 
                     * Aktionen, die eine Oberflächenänderung nach sich ziehen über Dispatcher
                     * verarbeitet werden.
                     */
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        Messages[i] = m;
                    }));
                }


            });

            /* Jede Nachricht die wir an den Server schicken wird mit einer "received" Nachricht beantwortet */
            socket.On("received", response =>
            {
                
                System.Diagnostics.Debug.WriteLine("received received from server");

                /* Die Antwort in unsere "ReadReceipt" Klasse umwandeln */
                MessageRecipt r = response.GetValue<MessageRecipt>();
                System.Diagnostics.Debug.WriteLine("received read from server {0}", r.UID);

                /*
                    Sobald uns der Server eine "Received" Bestätigung schickt suchen wir in unseren
                    Nachrichten nach der Nachricht mit der UID und setzen den "received" Wert auf WAHR
                 */
                Message m = Messages.Single(x => x.UID == r.UID);


                if (m != null)
                {
                    m.Received = true;
                    int i = Messages.IndexOf(m);

                    /* Oberfläche und SocketEvent laufen auf unterschiedlichen Threads, deshalb müssen 
                     * Aktionen, die eine Oberflächenänderung nach sich ziehen über Dispatcher
                     * verarbeitet werden.
                     */
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        Messages[i] = m;
                    }));
                }

            });

            /* SocketIO eingebaute Events */
            socket.OnError +=  (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            };

            /* Bei Verbindungsverlust versucht SocketIO automatisch wieder zu verbinden */
            socket.OnReconnectAttempt +=  (sender, e) =>
            {
                
                System.Diagnostics.Debug.WriteLine("Reconnect attempt");
            };

            socket.OnConnected +=  (sender, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Socketio is connected");

                /* Sobald wir mit dem Server verbunden sind senden wir ein 
                 * "Ping" Event und erwarten dafür eine "Pong" Antwort
                 */

                socket.EmitAsync("ping");


            };
          
            /* Socket Verbindung aufbauen */
            socket.ConnectAsync();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }



        private void SendMessage()
        {

            Message m = new Message(MessageDraft);

            /* Textbox leeren */
            MessageDraft = "";

            /* Nachricht der Liste hinzufügen */
            Messages.Add(m);

            /* Die Nachricht an den Server schicken */
            socket.EmitAsync("message", m);

        }


        /* "Senden" Button Event */
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {

            SendMessage();

        }

        /* "Enter" Event der Textbox */
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {

            if(e.Key == Key.Enter) { SendMessage(); }
           
        }
    }
}
