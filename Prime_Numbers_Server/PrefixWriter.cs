using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prime_Numbers_Server
{
    class PrefixedWriter : TextWriter
    {
        private TextWriter originalOut;

        public PrefixedWriter()
        {
            originalOut = Console.Out;
        }

        public override Encoding Encoding
        {
            get { return new System.Text.ASCIIEncoding(); }
        }
        public override void WriteLine(string message)
        {
            originalOut.WriteLine(String.Format("[{0:T}]: {1}", DateTime.Now, message));
        }
        /*
        public override void Write(string message)
        {
            originalOut.Write(String.Format("[{0:T}]: {1} \r\n", DateTime.Now, message));
        }
        */
    }
}
