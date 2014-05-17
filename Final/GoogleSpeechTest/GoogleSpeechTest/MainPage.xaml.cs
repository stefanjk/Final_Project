using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using GoogleSpeechTest.Resources;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Resources;
using System.Diagnostics;
using Newtonsoft.Json;
using Windows.Storage.Streams;
using libsound;
using libflac_wrapper;
using System.IO.IsolatedStorage;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Media;
using System.Net.Http;
//for using microphone
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System.Windows.Media.Imaging;
using System.Windows.Threading;


namespace GoogleSpeechTest
{
    // These classes made from http://json2csharp.com/
    public class Alternative
    {
        public string transcript { get; set; }
        public double confidence { get; set; }
    }

    public class Result
    {
        public List<Alternative> alternative { get; set; }
        public bool final { get; set; }
    }

    public class RecognitionResult
    {
        public List<Result> result { get; set; }
        public int result_index { get; set; }
    }

    public partial class MainPage : PhoneApplicationPage
    {

        Microphone microphone = Microphone.Default;
        byte[] buffer;
        MemoryStream stream = new MemoryStream();
        SoundEffect sound;
        SoundEffectInstance soundInstance;
        bool soundIsPlaying;
        
        // This is the object we'll record data into
        private libFLAC lf = new libFLAC();
        private SoundIO sio = new SoundIO();

        // This is our list of float[] chunks that we're keeping track of
        private List<float[]> recordedAudio = new List<float[]>();

        // This is our flag as to whether or not we're currently recording
        private bool recording = false;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            // Timer to simulate the XNA Framework game loop (Microphone is  
            // from the XNA Framework). We also use this timer to monitor the  
            // state of audio playback so we can update the UI appropriately. 
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(33);
            dt.Tick += new EventHandler(dt_Tick);
            dt.Start();


            // Event handler for getting audio data when the buffer is full 
            microphone.BufferReady += new EventHandler<EventArgs>(microphone_BufferReady);


        }

      
        // Updates the XNA FrameworkDispatcher and checks to see if a sound is playing. 
        // If sound has stopped playing, it updates the UI. 
        void dt_Tick(object sender, EventArgs e)
        {
            try { FrameworkDispatcher.Update(); }
            catch { }

            if (true == soundIsPlaying)
            {
                if (soundInstance.State != SoundState.Playing)
                {
                    // Audio has finished playing 
                    soundIsPlaying = false;

                    // Update the UI to reflect that the  
                    // sound has stopped playing 
                    SetButtonStates(true, true, false);
                    textOutput.Text = "press play\nor record";
                 
                }
            }
        }
 
        // The Microphone.BufferReady event handler. 
        // Gets the audio data from the microphone and stores it in a buffer, 
        // then writes that buffer to a stream for later playback. 
        // Any action in this event handler should be quick! 
        void microphone_BufferReady(object sender, EventArgs e)
        {
            // Retrieve audio data 
            microphone.GetData(buffer);

            // Store the audio data in a stream 
            stream.Write(buffer, 0, buffer.Length);
        }

        // Handles the Click event for the record button. 
        // Sets up the microphone and data buffers to collect audio data, 
        // then starts the microphone. Also, updates the UI. 
        private void recordButton_Click(object sender, EventArgs e)
        {
            // Get audio data in 1/2 second chunks 
            microphone.BufferDuration = TimeSpan.FromMilliseconds(500);

            // Allocate memory to hold the audio data 
            buffer = new byte[microphone.GetSampleSizeInBytes(microphone.BufferDuration)];

            // Set the stream back to zero in case there is already something in it 
            stream.SetLength(0);

            // Start recording 
            microphone.Start();

            SetButtonStates(false, false, true);
            textOutput.Text = "record";
        }

        // Handles the Click event for the stop button. 
        // Stops the microphone from collecting audio and updates the UI. 
        private void stopButton_Click(object sender, EventArgs e)
        {
            if (microphone.State == MicrophoneState.Started)
            {
                // In RECORD mode, user clicked the  
                // stop button to end recording 
                microphone.Stop();
            }
            else if (soundInstance.State == SoundState.Playing)
            {
                // In PLAY mode, user clicked the  
                // stop button to end playing back 
                soundInstance.Stop();
            }

            SetButtonStates(true, true, false);
            textOutput.Text = "ready";
        }
 
        // Handles the Click event for the play button. 
        // Plays the audio collected from the microphone and updates the UI. 
        private void playButton_Click(object sender, EventArgs e)
        {
            if (stream.Length > 0)
            {
                // Update the UI to reflect that 
                // sound is playing 
                SetButtonStates(false, false, true);
                textOutput.Text = "play";
          

                // Play the audio in a new thread so the UI can update. 
                Thread soundThread = new Thread(new ThreadStart(playSound));
                soundThread.Start();
            }
        }
        
        // Plays the audio using SoundEffectInstance  
        // so we can monitor the playback status.
        private void playSound()
        {
            // Play audio using SoundEffectInstance so we can monitor it's State  
            // and update the UI in the dt_Tick handler when it is done playing. 
            SoundEffect sound = new SoundEffect(stream.ToArray(), microphone.SampleRate, AudioChannels.Mono);
            soundInstance = sound.CreateInstance();
            soundIsPlaying = true;
            soundInstance.Play();
        }


        /// Helper method to change the IsEnabled property for the ApplicationBarIconButtons. 
        private void SetButtonStates(bool recordEnabled, bool playEnabled, bool stopEnabled)
        {
            (ApplicationBar.Buttons[0] as ApplicationBarIconButton).IsEnabled = recordEnabled;
            (ApplicationBar.Buttons[1] as ApplicationBarIconButton).IsEnabled = playEnabled;
            (ApplicationBar.Buttons[2] as ApplicationBarIconButton).IsEnabled = stopEnabled;
        } 




        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Setup SoundIO right away
            sio.audioInEvent += sio_audioInEvent;
            sio.start();
        }

        void sio_audioInEvent(float[] data)
        {
            // Only do something if we're recording right now
            if (this.recording)
            {
                // If we are recording, throw our data into our recordedAudio list
                recordedAudio.Add(data);

                // Update progress bar
                Dispatcher.BeginInvoke(() =>
                {
                    progress.Value = recordedAudio.Count / 10.0;
                });

                // If we're reached our maximum recording limit....
                if (recordedAudio.Count == 1000)
                {
                    // We stop ourselves! :P
                    stopRecording();
                }
            }
        }

        // This gets called when the button gets pressed while it says "Go"
        private void startRecording()
        {
            this.recording = true;
            this.goButton.Content = "Stop";
            this.textOutput.Text = "Recording...";
            startCollection();
        }

        // This gets called when the button gets pressed while it says "Stop" or when we reach
        // our maximum buffer amount (set to 10 seconds right now)
        private void stopRecording()
        {
            this.recording = false;

            // Do this in a Dispatcher.BeginInvoke since we're not certain which thread is calling this function!
            Dispatcher.BeginInvoke(() =>
            {
                this.textOutput.Text = "Processing...";
                this.progress.Value = 0;
                this.goButton.Content = "Go";
                endCollection();
                processData();
            });
        }

        // This is a utility to take a list of arrays and mash them all together into one large array
        private T[] flattenList<T>(List<T[]> list)
        {
            // Calculate total size
            int size = 0;
            foreach (var el in list)
            {
                size += el.Length;
            }

            // Allocate the returning array
            T[] ret = new T[size];

            // Copy each chunk into this new array
            int idx = 0;
            foreach (var el in list)
            {
                el.CopyTo(ret, idx);
                idx += el.Length;
            }

            // Return the "flattened" array
            return ret;
        }

        private async void processData()
        {
            // First, convert our list of audio chunks into a flattened single array
            float[] rawData = flattenList(recordedAudio);

            // Once we've done that, we can clear this out no problem
            recordedAudio.Clear();

            // Next, convert the data into FLAC:
            byte[] flacData = null;
            flacData = lf.compressAudio(rawData, sio.getInputSampleRate(), sio.getInputNumChannels());
            
            // Upload it to the server and get a response!
            RecognitionResult result = await recognizeSpeech(flacData, sio.getInputSampleRate());

            // Check to make sure everything went okay, if it didn't, check the debug log!
            if (result.result.Count != 0)
            {
                // This is just some fancy code to display each hypothesis as sone text that gets redder
                // as our confidence goes down; note that I've never managed to get multiple hypotheses
                this.textOutput.Inlines.Clear();
                foreach (var alternative in result.result[0].alternative)
                {
                    Run run = new Run();
                    run.Text = alternative.transcript + "\n\n";
                    byte bg = (byte)(alternative.confidence * 255);
                    run.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, bg, bg));
                    textOutput.Inlines.Add(run);
                }
            }
            else
            {
                textOutput.Text = "Errored out!";
            }
        }

        private async Task<RecognitionResult> recognizeSpeech(byte[] flacData, uint sampleRate)
        {
            try
            {
                // Construct our HTTP request to the server
                string url = "https://www.google.com/speech-api/v2/recognize?output=json&lang=en-us&key=AIzaSyCnl6MRydhw_5fLXIdASxkLJzcJh5iX0M4";
                HttpWebRequest request = WebRequest.CreateHttp(url);

                // Make sure we tell it what kind of data we're sending
                request.ContentType = "audio/x-flac; rate=" + sampleRate;
                request.Method = "POST";

                // Actually write the data out to the stream!
                using (var stream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
                {
                    await stream.WriteAsync(flacData, 0, flacData.Length);
                }

                // We are going to store our json response into this RecognitionResult:
                RecognitionResult root = null;

                // Now, we wait for a response and read it in:
                using (var response = await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {
                    // Construct a datareader so we can read everything in as a string
                    DataReader dr = new DataReader(response.GetResponseStream().AsInputStream());

                    dr.InputStreamOptions = InputStreamOptions.Partial;

                    uint datalen = await dr.LoadAsync(1024 * 1024);
                    string responseStringsCombined = dr.ReadString(datalen);

                    // Split this response string by its newlines
                    var responseStrings = responseStringsCombined.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

                    // Now, inspect the JSON of each string
                    foreach (var responseString in responseStrings)
                    {
                        root = JsonConvert.DeserializeObject<RecognitionResult>(responseString);

                        // If this is a good result
                        if (root.result.Count != 0)
                        {
                            //return it!
                            return root;
                        }
                    }
                }

                // Aaaaand, return the root object!
                return root;
            }
            catch(Exception e)
            {
                Debug.WriteLine("Error detected!  Exception thrown: " + e.Message);
            }

            // Otherwise, something failed, and we don't know what!
            return new RecognitionResult();
        }

        private void goButton_Click(object sender, RoutedEventArgs e)
        {
       /*     if( this.recording ) {
                stopRecording();
            } else {
                startRecording();
            }
        */
        }

        //new functions to save data to storage
        private void SaveToIsolatedStorage()
        {
            // first, we grab the current apps isolated storage handle
            IsolatedStorageFile isf = IsolatedStorageFile.GetUserStoreForApplication();

            // we give our file a filename
            string strSaveName = "myFile.wav";

            // if that file exists... 
            if (isf.FileExists(strSaveName))
            {
                // then delete it
                isf.DeleteFile(strSaveName);
            }

            // now we set up an isolated storage stream to point to store our data
            IsolatedStorageFileStream isfStream =
                     new IsolatedStorageFileStream(strSaveName,
                     FileMode.Create, IsolatedStorageFile.GetUserStoreForApplication());

            isfStream.Write(stream.ToArray(), 0, stream.ToArray().Length);

            // ok, done with isolated storage... so close it
            isfStream.Close();
        }

        public void UpdateWavHeader(Stream stream)
        {
            if (!stream.CanSeek) throw new Exception("Can't seek stream to update wav header");

            var oldPos = stream.Position;

            // ChunkSize 36 + SubChunk2Size
            stream.Seek(4, SeekOrigin.Begin);
            stream.Write(BitConverter.GetBytes((int)stream.Length - 8), 0, 4);

            // Subchunk2Size == NumSamples * NumChannels * BitsPerSample/8 This is the number of bytes in the data.
            stream.Seek(40, SeekOrigin.Begin);
            stream.Write(BitConverter.GetBytes((int)stream.Length - 44), 0, 4);

            stream.Seek(oldPos, SeekOrigin.Begin);
        }

        public void WriteWavHeader(Stream stream, int sampleRate)
        {
            const int bitsPerSample = 16;
            const int bytesPerSample = bitsPerSample / 8;
            var encoding = System.Text.Encoding.UTF8;

            // ChunkID Contains the letters "RIFF" in ASCII form (0x52494646 big-endian form).
            stream.Write(encoding.GetBytes("RIFF"), 0, 4);

            // NOTE this will be filled in later
            stream.Write(BitConverter.GetBytes(0), 0, 4);

            // Format Contains the letters "WAVE"(0x57415645 big-endian form).
            stream.Write(encoding.GetBytes("WAVE"), 0, 4);

            // Subchunk1ID Contains the letters "fmt " (0x666d7420 big-endian form).
            stream.Write(encoding.GetBytes("fmt "), 0, 4);

            // Subchunk1Size 16 for PCM. This is the size of therest of the Subchunk which follows this number.
            stream.Write(BitConverter.GetBytes(16), 0, 4);

            // AudioFormat PCM = 1 (i.e. Linear quantization) Values other than 1 indicate some form of compression.
            stream.Write(BitConverter.GetBytes((short)1), 0, 2);

            // NumChannels Mono = 1, Stereo = 2, etc.
            stream.Write(BitConverter.GetBytes((short)1), 0, 2);

            // SampleRate 8000, 44100, etc.
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);

            // ByteRate = SampleRate * NumChannels * BitsPerSample/8
            stream.Write(BitConverter.GetBytes(sampleRate * bytesPerSample), 0, 4);

            // BlockAlign NumChannels * BitsPerSample/8 The number of bytes for one sample including all channels.
            stream.Write(BitConverter.GetBytes((short)(bytesPerSample)), 0, 2);

            // BitsPerSample 8 bits = 8, 16 bits = 16, etc.
            stream.Write(BitConverter.GetBytes((short)(bitsPerSample)), 0, 2);

            // Subchunk2ID Contains the letters "data" (0x64617461 big-endian form).
            stream.Write(encoding.GetBytes("data"), 0, 4);

            // NOTE to be filled in later
            stream.Write(BitConverter.GetBytes(0), 0, 4);
        }

        private void startCollection()
{
            
            stream.SetLength(0);
 
            WriteWavHeader(stream, microphone.SampleRate);
 
            // Start recording
            microphone.Start();
            
}

        private void endCollection()
        {
            microphone.Stop();
            UpdateWavHeader(stream);
            SaveToIsolatedStorage();

        }

        private void playButton_Click(object sender, RoutedEventArgs e)
        {

        }


    }
}