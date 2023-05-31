using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CsvHelper;
using RestSharp;

namespace Rock_AI_ElevenLabs_winform
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        //initialize variables
        private Stream stream;
        private OpenFileDialog dialog = new OpenFileDialog();
        private string fileLocation;
        private string fileName;
        private string fileDirectory;

        //click to load in a CSV file
        private void button1_Click( object sender, EventArgs e )
        {
            //run a try catch to catch if the user selects a file that is open
            try
            {
                //set a filter so that only .csv files can be selected
                dialog.Filter = "CSV Files|*.csv";
                //check if the result is a file
                if ( dialog.ShowDialog() == DialogResult.OK )
                {
                    if ( ( stream = dialog.OpenFile() ) != null )
                    {
                        //set the file location and name of the file chosen by the user
                        fileLocation = dialog.FileName;
                        label2.Text = dialog.SafeFileName;
                        fileDirectory = Path.GetDirectoryName( fileLocation );
                        fileName = Path.GetFileNameWithoutExtension( dialog.FileName );
                        label3.Text = "";
                    }
                }
            }
            catch ( System.IO.IOException )
            {
                label3.Text = "Please Select A File that is not open.";
            }
        }

        //click to read in a column of a CSV file and convert it to individual audio files
        private async void button2_Click( object sender, EventArgs e )
        {
            //collect user data from form
            string apiKey = textBox1.Text;
            string columnHeader = textBox2.Text;

            //check if a CSV file has been selected
            if ( String.IsNullOrWhiteSpace( fileLocation ) )
            {
                label3.Text = "Please Select a CSV file.";
                return;
            }
            //check if apikey has been included
            if ( String.IsNullOrWhiteSpace( apiKey ) )
            {
                label3.Text = "Please include an API key.";
                return;
            }
            //check if column header has been specified
            if ( String.IsNullOrWhiteSpace( columnHeader ) )
            {
                label3.Text = "Please specify a column header from your CSV file.";
                return;
            }
            //create a rest client
            var client = new RestClient( "https://api.elevenlabs.io/v1" );

            //read in cvs header row by row
            int totalRecords = 0;
            List<string> chosenColumn = new List<string>();
            using ( var fileReader = new StreamReader( fileLocation ) )
            using ( var csvResult = new CsvReader( fileReader, CultureInfo.InvariantCulture ) )
            {
                //read in header of csv file
                csvResult.Read();
                csvResult.ReadHeader();

                //while there is still data read only the selected column specified
                while ( csvResult.Read())
                {
                    string stringField = csvResult.GetField<string>( columnHeader );
                    chosenColumn.Add( stringField );
                    totalRecords++;
                }
            }

            //remove all duplicates in the list of passed data to save audio from being remade for the same text
            List<string> distinctItems = chosenColumn
                                    .GroupBy( i => i.ToString())
                                    .Select( g => g.First() )
                                    .ToList();

            //create an api call for each value in the distinct list
            int recordCount = 1;
            foreach (string textValue in distinctItems)
            {
                label3.Text = "Reading Record " + recordCount + " out of " + totalRecords + " records.";
                bool readable = true;

                //check if there is a passable value to elevenLabs
                if (textValue.Equals("") || textValue.Contains("["))
                {
                    readable = false;
                }

                //if the text can be read and passed to eleven Labs then do the call
                if ( readable )
                {
                    //create new REST api call
                    string voiceId = "ErXwobaYiN019PkySvjV"; //the voice id is set to Antoni on ElevenLabs
                    var request = new RestRequest( "text-to-speech/" + voiceId + "/stream", Method.Post );
                    request.Timeout = 180000;

                    //add a header to api call
                    request.AddHeader( "xi-api-key", apiKey );

                    //add the body of the HTML POST request which is detailed in the eleven labs documentation
                    string modelID = "eleven_monolingual_v1";
                    request.AddJsonBody( new
                    {
                        text = textValue,
                        model_id = modelID,
                        voice_settings = new
                        {
                            stability = "0.5",
                            similarity_boost = "0.5"
                        }
                    } );

                    //create a response for the REST call
                    var response = new RestSharp.RestResponse();
                    try
                    {
                        response = await client.PostAsync( request );
                    }
                    catch (System.Net.Http.HttpRequestException)
                    {
                        label4.Text += "Could not create audio for record " +recordCount+ ".\n";
                    }
                    if ( response.StatusCode == System.Net.HttpStatusCode.OK )
                    {
                        // If a File.csv__output directory does not exist, create it
                        if ( !Directory.Exists( fileDirectory + "\\" + fileName + "CSV_" + columnHeader + "__output" ) )
                        {
                            Directory.CreateDirectory( fileDirectory + "\\" + fileName + "CSV_" + columnHeader + "__output" );
                        }

                        // Open a file stream and save the response content to a file
                        string oututFilepath = fileDirectory + "\\" + fileName + "CSV_" + columnHeader + "__output\\" + textValue + ".mp3";
                        File.WriteAllBytes( oututFilepath, response.RawBytes );
                        recordCount++;
                    }
                    else
                    {
                        label3.Text = response.ErrorMessage;
                    }
                }
                //if there is no data increase the record count and do nothing
                else
                {
                    recordCount++;
                }
                
            }

            //display where the output has been stored
            label3.Text = "New Directory Created With Output Files -->  " + fileDirectory + "\\" + fileName + "CSV_" + columnHeader + "__output";
        }
    }
}
