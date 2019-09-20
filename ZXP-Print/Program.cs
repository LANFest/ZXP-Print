using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using ZMOTIFPRINTERLib;
using ZMTGraphics;
using System.Drawing;
using System.IO;
using System.Threading;

namespace ZXP_Print
{
    class Program
    {
        static private short _alarm = 0;

        private struct JobStatusStruct
        {
            public int copiesCompleted,
                          copiesRequested,
                          errorCode;
            public string cardPosition,
                          contactlessStatus,
                          contactStatus,
                          magStatus,
                          printingStatus,
                          uuidJob;
        }


        static void Main(string[] args)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            Console.WriteLine("Listening..");
            listener.Start();

            // Run Once Stuff

            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                if (request.HttpMethod == "OPTIONS")
                {
                    response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                    response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
                    response.AddHeader("Access-Control-Max-Age", "1728000");
                }
                response.AppendHeader("Access-Control-Allow-Origin", "*");

                string responseString = "Beep";

                string raw_post = null;
                using (var reader = new StreamReader(request.InputStream,
                                                     request.ContentEncoding))
                {
                    raw_post = reader.ReadToEnd();
                }


                Console.WriteLine(raw_post);


                if (raw_post != "")
                {

                    byte[] _bmpFront = ImageToByteArray(raw_post);

                    int actionID = 0;

                    Job job = null;

                    try
                    {
                        job = new Job();
                        job.Open("06C103300020");

                        job.JobControl.FeederSource = FeederSourceEnum.CardFeeder;
                        job.JobControl.Destination = DestinationTypeEnum.Eject;

                        //job.BuildGraphicsLayers(SideEnum.Front, PrintTypeEnum.Color, 0, 0, 0, -1, GraphicTypeEnum.BMP, _bmpFront);
                        //job.BuildGraphicsLayers(SideEnum.Back, PrintTypeEnum.MonoK, 0, 0, 0, -1, GraphicTypeEnum.BMP, _bmpBack);
                        job.BuildGraphicsLayers(SideEnum.Back, PrintTypeEnum.Color, 0, 0, 0, -1, GraphicTypeEnum.BMP, _bmpFront);

                        int copies = 1;

                        job.PrintGraphicsLayers(copies, out actionID);

                        job.ClearGraphicsLayers();

                        string status = string.Empty;
                        JobWait(ref job, actionID, 180, out status);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                else
                {
                    responseString = "No URL in Body";

                }





                Console.WriteLine("Boop!");
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }

         }

    
        // Loads a byte array with image data from a file
        // --------------------------------------------------------------------------------------------------

         static private byte[] ImageToByteArray(string filename)
        {
            //Image img = System.Drawing.Image.FromFile(filename);
            WebClient wc = new WebClient();
            byte[] bytes = wc.DownloadData(filename);

            MemoryStream ms = new MemoryStream(bytes);
            System.Drawing.Image img = System.Drawing.Image.FromStream(ms);
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
            return ms.ToArray();
        }

        // Waits for a job to complete
        // --------------------------------------------------------------------------------------------------

        static public void JobWait(ref Job job, int actionID, int loops, out string status)
        {
            status = string.Empty;

            try
            {
                JobStatusStruct js = new JobStatusStruct();

                while (loops > 0)
                {
                    try
                    {
                        _alarm = job.GetJobStatus(actionID, out js.uuidJob, out js.printingStatus,
                                    out js.cardPosition, out js.errorCode, out js.copiesCompleted,
                                    out js.copiesRequested, out js.magStatus, out js.contactStatus,
                                    out js.contactlessStatus);

                        if (js.printingStatus == "done_ok" || js.printingStatus == "cleaning_up")
                        {
                            status = js.printingStatus + ": " + "Indicates a job completed successfully";
                            break;
                        }
                        else if (js.printingStatus.Contains("cancelled"))
                        {
                            status = js.printingStatus;
                            break;
                        }

                        if (js.contactStatus.ToLower().Contains("error"))
                        {
                            status = js.contactStatus;
                            break;
                        }

                        if (js.printingStatus.ToLower().Contains("error"))
                        {
                            status = "Printing Status Error";
                            break;
                        }

                        if (js.contactlessStatus.ToLower().Contains("error"))
                        {
                            status = js.contactlessStatus;
                            break;
                        }

                        if (js.magStatus.ToLower().Contains("error"))
                        {
                            status = js.magStatus;
                            break;
                        }

                        if (_alarm != 0 && _alarm != 4016) //no error or out of cards
                        {
                            status = "Error: " + job.Device.GetStatusMessageString(_alarm);
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        status = "Job Wait Exception: " + e.Message;
                        break;
                    }

                    if (_alarm == 0)
                    {
                        if (--loops <= 0)
                        {
                            status = "Job Status Timeout";
                            break;
                        }
                    }
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                Console.WriteLine(status);
            }
        }


    }

}
