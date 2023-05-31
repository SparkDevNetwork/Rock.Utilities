using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CsvHelper;
using RestSharp;
using RestSharp.Authenticators;

namespace Rock_AI_Helper_Windows_Form
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        
        private void Form1_Load( object sender, EventArgs e )
        {

        }

        //initialize variables
        private Stream stream;
        private OpenFileDialog dialog = new OpenFileDialog();
        private string fileLocation;
        private string fileName;

        //select a CSV file to be read from
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
                        label4.Text = dialog.SafeFileName;
                        fileName = Path.GetFileNameWithoutExtension( dialog.FileName );
                        label5.Text = "";
                    }
                }
            }
            catch (System.IO.IOException)
            {
                label5.Text = "Please Select A File that is not open.";
            }
        }

        //run the command through openAI and save the new data
        private async void button2_Click( object sender, EventArgs e )
        { 
            //collect data from the user
            string apiKey = textBox2.Text;
            string orgId = textBox3.Text;

            //check if api key has been plugged in
            if ( apiKey.Equals(""))
            {
                label6.Text = "Please Enter an API Key";
                return;
            }

            //check if a CSV file has been selected
            if ( String.IsNullOrWhiteSpace(fileLocation))
            {
                label6.Text = "Please Select a CSV file";
                return;
            }
            
            //declare the client
            var options = new RestClientOptions( "https://api.openai.com/v1" )
            {
                Authenticator = new JwtAuthenticator( apiKey )
            };
            var client = new RestClient( options );
            

            //declare the gpt chat model and chat content
            string chatModel = "gpt-4";
            string chatContent = textBox1.Text;

            //parse the textbox chat
            if ( chatContent.Contains( "{{" ) && chatContent.Contains("}}"))
            {
                label6.Text = "";
                //grab the parameter that has been passed in the chatContent
                int paramStartIndex = chatContent.IndexOf( "{{" ) + 2;
                int paramEndIndex = chatContent.IndexOf( "}}" );
                string paramName = chatContent.Substring( paramStartIndex, paramEndIndex - paramStartIndex );
                string tempChat = "";

                //created csvReader and cvsWriter to read and write to the new csv file
                string fileDirectory = Path.GetDirectoryName( fileLocation );
                string createdCSVName = fileDirectory + "\\" + fileName + "_output.csv";
                using ( var reader = new StreamReader( fileLocation ) )
                using ( var csvReader = new CsvReader( reader, CultureInfo.InvariantCulture ) )
                using ( var writer = new StreamWriter( createdCSVName ) )
                using ( var csvWriter = new CsvWriter( writer, CultureInfo.InvariantCulture ) )
                {
                    //read in header file
                    csvReader.Read();
                    csvReader.ReadHeader();

                    // Write headers and add a new header for the new data
                    for ( int i = 0; i < csvReader.Context.Parser.Record.Length; i++ )
                    {
                        csvWriter.WriteField( csvReader.Context.Parser.Record[i] );
                    }
                    csvWriter.WriteField( paramName + "_Output" );
                    csvWriter.NextRecord();

                    //count the total number of records
                    int totalRecords = 0;
                    using ( var numRecordsReader = new StreamReader( fileLocation ) )
                    using ( var numRecordsCSVReader = new CsvReader( numRecordsReader, CultureInfo.InvariantCulture ) )
                    {
                        numRecordsCSVReader.Read();
                        numRecordsCSVReader.ReadHeader();

                        //loop till the end of records
                        while ( numRecordsCSVReader.Read() )
                        {
                            totalRecords++;
                        }
                    }

                    //loop until the reader reaches the end of reading
                    int recordCount = 1;
                    while ( csvReader.Read() )
                    {
                        string chatGPTAnswer = "";
                        bool ableToRead = true;
                        label5.Text = "Reading Record " + recordCount + " out of " + totalRecords + " records.";
                        
                        //replace the parameter variable with the actual data
                        tempChat = chatContent.Replace( "{{" + paramName + "}}", csvReader.GetField<string>( paramName ) );

                        //check if the field has a value
                        if ( csvReader.GetField<string>( paramName ).Equals(""))
                        {
                            chatGPTAnswer = "[NO " + paramName.ToUpper() + "]";
                            ableToRead = false;
                        }

                        //make sure there is a value being passed to chatGPT
                        if ( ableToRead )
                        {
                            //create new REST api call
                            var request = new RestRequest( "chat/completions", Method.Post );
                            request.Timeout = 180000;
                            request.AddHeader( "OpenAI-Organization", orgId );

                            //add the body of the HTML POST request
                            request.AddJsonBody( new
                            {
                                model = chatModel,
                                messages = new[] {
                                new{
                                    role = "user",
                                    content = tempChat
                                }}
                            });
                            

                            //create a respose for the REST call
                            var response = new RestSharp.RestResponse();

                            //run a try catch to see if chatGPT times out on request
                            try
                            {
                                response = await client.PostAsync( request );
                            }
                            catch ( System.Net.Http.HttpRequestException httpEx )
                            {
                                chatGPTAnswer = "[ERROR]";
                                label6.Text += "Could Not Read Record " + recordCount + "\n";
                            }
                            

                            if ( response.StatusCode == System.Net.HttpStatusCode.OK )
                            {
                                //deserialize the json return value and get the required content
                                string responseString = response.Content;
                                var responseJson = JsonSerializer.Deserialize<JsonNode>( response.Content );
                                chatGPTAnswer = ( string ) responseJson["choices"][0]["message"]["content"];
                            }
                            else
                            {
                                label5.Text = response.ErrorMessage;
                            }
                        }

                        //write the existing data into new CSV file 
                        for ( int i = 0; i < csvReader.Context.Parser.Record.Length; i++ )
                        {
                            csvWriter.WriteField( csvReader.Context.Parser.Record[i] );
                        }

                        // Write new chatGPTdata
                        csvWriter.WriteField( chatGPTAnswer );
                        csvWriter.NextRecord();
                        recordCount++;
                    }
                }

                //update label 5
                label5.Text = "New File With Output Written -->  " + createdCSVName;
            }
            else
            {
                label5.Text = "Request does not contain variable. Example: {{columnHeaderHere}}";
            }
        }
    }
}
