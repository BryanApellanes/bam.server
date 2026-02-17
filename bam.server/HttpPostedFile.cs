using System.Text;
using Bam.ServiceProxy;

namespace Bam.Server
{
    /// <summary>
    /// Represents a file posted via an HTTP multipart form upload. Parses multipart boundary-delimited content
    /// to extract file data and metadata.
    /// </summary>
    public class HttpPostedFile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPostedFile"/> class with the specified encoding, boundary, and input stream.
        /// </summary>
        /// <param name="encoding">The encoding used to interpret the multipart content.</param>
        /// <param name="boundary">The multipart boundary string.</param>
        /// <param name="input">The input stream containing the multipart data.</param>
        public HttpPostedFile(Encoding encoding, string boundary, Stream input)
        {
            Encoding = encoding;
            Boundary = boundary;
            InputStream = input;
            QueryString = new Dictionary<string, string>();
        }

        /// <summary>
        /// Creates an <see cref="HttpPostedFile"/> from the specified request, extracting the boundary and query string parameters.
        /// </summary>
        /// <param name="request">The HTTP request containing the multipart upload.</param>
        /// <returns>A new <see cref="HttpPostedFile"/> populated with data from the request.</returns>
        public static HttpPostedFile FromRequest(IRequest request)
        {
            HttpPostedFile file = new HttpPostedFile(request.ContentEncoding, GetBoundary(request), request.InputStream);
            foreach (string key in request.QueryString.Keys)
            {
                file.QueryString.Add(key, request.QueryString[key]!);
            }
            return file;
        }

        private static string GetBoundary(IRequest request)
        {
            return "--" + request.ContentType.Split(';')[1].Split('=')[1];
        }

        /// <summary>
        /// Gets the query string parameters from the original request.
        /// </summary>
        public Dictionary<string, string> QueryString
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the temporary file path where the uploaded file content was saved.
        /// </summary>
        public string TempPath
        {
            get;
            private set;
        } = null!;

        /// <summary>
        /// Gets or sets the full file path of the uploaded file.
        /// </summary>
        public string FullPath
        {
            get;
            internal set;
        } = null!;

        /// <summary>
        /// Gets or sets the original file name from the multipart content disposition.
        /// </summary>
        public string FileName
        {
            get; set;
        } = null!;

        /// <summary>
        /// Gets or sets the name of the form input field that submitted the file.
        /// </summary>
        public string FormInputName
        {
            get; set;
        } = null!;

        /// <summary>
        /// Gets or sets the MIME content type of the uploaded file.
        /// </summary>
        public string ContentType
        {
            get; set;
        } = null!;

        /// <summary>
        /// Reads the content disposition metadata (form input name, file name, and content type) from the input stream.
        /// </summary>
        public void ReadMeta()
        {
            MemoryStream copy = CopyInputStream();
            string dispo = string.Empty;
            using (StreamReader sr = new StreamReader(copy))
            {
                string discard = sr.ReadLine()!;
                dispo = sr.ReadLine()!;
                ContentType = sr.ReadLine()!.DelimitSplit(":")[1];
            }
            string[] meta = dispo.DelimitSplit(":")[1].DelimitSplit(";");
            FormInputName = meta[1].DelimitSplit("=")[1].TruncateFront(1).Truncate(1);
            FileName = meta[2].DelimitSplit("=")[1].TruncateFront(1).Truncate(1);
        }

        protected internal void Save(string path)
        {
            if (string.IsNullOrEmpty(TempPath))
            {
                TempPath = path;
                Encoding enc = Encoding;
                string boundary = Boundary;
                InputStream = CopyInputStream();
                Stream input = InputStream;
                byte[] boundaryBytes = enc.GetBytes(boundary);
                int boundaryLen = boundaryBytes.Length;

                using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[1024];
                    int len = input.Read(buffer, 0, 1024);
                    int startPos = -1;

                    // Find start boundary
                    while (true)
                    {
                        if (len == 0)
                        {
                            throw new InvalidOperationException("Start Boundaray Not Found");
                        }

                        startPos = IndexOf(buffer, len, boundaryBytes);
                        if (startPos >= 0)
                        {
                            break;
                        }
                        else
                        {
                            Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                            len = input.Read(buffer, boundaryLen, 1024 - boundaryLen);
                        }
                    }

                    // Skip four lines (Boundary, Content-Disposition, Content-Type, and a blank)
                    for (int i = 0; i < 4; i++)
                    {
                        while (true)
                        {
                            if (len == 0)
                            {
                                throw new InvalidOperationException("Preamble not Found.");
                            }

                            startPos = Array.IndexOf(buffer, enc.GetBytes("\n")[0], startPos);
                            if (startPos >= 0)
                            {
                                startPos++;
                                break;
                            }
                            else
                            {
                                len = input.Read(buffer, 0, 1024);
                            }
                        }
                    }

                    Array.Copy(buffer, startPos, buffer, 0, len - startPos);
                    len = len - startPos;

                    while (true)
                    {
                        int endPos = IndexOf(buffer, len, boundaryBytes);
                        if (endPos >= 0)
                        {
                            if (endPos > 0) output.Write(buffer, 0, endPos);
                            break;
                        }
                        else if (len <= boundaryLen)
                        {
                            throw new InvalidOperationException("End Boundaray Not Found");
                        }
                        else
                        {
                            output.Write(buffer, 0, len - boundaryLen);
                            Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                            len = input.Read(buffer, boundaryLen, 1024 - boundaryLen) + boundaryLen;
                        }
                    }
                }
                ReadMeta();
            }
        }

        private MemoryStream CopyInputStream()
        {
            if (InputStream.CanSeek)
            {
                InputStream.Seek(0, SeekOrigin.Begin);
            }
            MemoryStream copy = new MemoryStream();
            InputStream.CopyTo(copy);
            copy.Seek(0, SeekOrigin.Begin);
            return copy;
        }

        protected Encoding Encoding { get; private set; }
        protected string Boundary { get; private set; }
        protected Stream InputStream { get; private set; }

        private static int IndexOf(byte[] buffer, int len, byte[] boundaryBytes)
        {
            for (int i = 0; i <= len - boundaryBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < boundaryBytes.Length && match; j++)
                {
                    match = buffer[i + j] == boundaryBytes[j];
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
