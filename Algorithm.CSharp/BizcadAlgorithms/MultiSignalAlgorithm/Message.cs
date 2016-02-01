using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp
{
    public class Message
    {
        /// <summary>
        /// The Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// A message type so it can map to headers and get an instantiation type
        /// </summary>
        public string MessageType { get; set; }
        /// <summary>
        /// The contents of the message.
        /// </summary>
        public string Contents { get; set; }

    }
}
