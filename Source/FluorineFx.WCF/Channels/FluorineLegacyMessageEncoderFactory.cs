/*
	FluorineFx open source library 
	Copyright (C) 2010 Zoltan Csibi, zoltan@TheSilentGroup.com, FluorineFx.com 
	
	This library is free software; you can redistribute it and/or
	modify it under the terms of the GNU Lesser General Public
	License as published by the Free Software Foundation; either
	version 2.1 of the License, or (at your option) any later version.
	
	This library is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
	Lesser General Public License for more details.
	
	You should have received a copy of the GNU Lesser General Public
	License along with this library; if not, write to the Free Software
	Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
*/
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.Xml;
using System.ServiceModel.Security;
using System.ServiceModel.Description;
using FluorineFx;
using FluorineFx.IO;

namespace FluorineFx.WCF.Channels
{
    /// <summary>
    /// This class is used to create the custom encoder (FluorineMessageEncoder)
    /// </summary>
    class FluorineLegacyMessageEncoderFactory : MessageEncoderFactory
    {
        MessageEncoder encoder;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluorineLegacyMessageEncoderFactory"/> class.
        /// </summary>
        public FluorineLegacyMessageEncoderFactory()
        {
            encoder = new FluorineLegacyMessageEncoder();

        }

        /// <summary>
        /// The service framework uses this property to obtain an encoder from this encoder factory.
        /// </summary>
        public override MessageEncoder Encoder
        {
            get { return encoder; }
        }

        /// <summary>
        /// When overridden in a derived class, gets the message version that is used by the encoders produced by the factory to encode messages.
        /// </summary>
        /// <returns>The <see cref="T:System.ServiceModel.Channels.MessageVersion"/> used by the factory.</returns>
        public override MessageVersion MessageVersion
        {
            get { return encoder.MessageVersion; }
        }


        /// <summary>
        /// This is the actual AMF encoder.
        /// </summary>
        class FluorineLegacyMessageEncoder : MessageEncoder
        {
            static string AmfContentType = "application/x-amf";

            internal FluorineLegacyMessageEncoder()
                : base()
            {
            }

            public override string ContentType
            {
                get { return AmfContentType; }
            }

            public override string MediaType
            {
                get { return AmfContentType; }
            }

            //SOAP version to use - we delegate to the inner encoder for this
            public override MessageVersion MessageVersion
            {
                get { return MessageVersion.None; }
            }


            /// <summary>
            /// One of the two main entry points into the encoder. Called by WCF to decode a buffered byte array into a message.
            /// </summary>
            /// <param name="buffer">A <see cref="T:System.ArraySegment`1"/> of type <see cref="T:System.Byte"/> that provides the buffer from which the message is deserialized.</param>
            /// <param name="bufferManager">The <see cref="T:System.ServiceModel.Channels.BufferManager"/> that manages the buffer from which the message is deserialized.</param>
            /// <param name="contentType">The Multipurpose Internet Mail Extensions (MIME) message-level content-type.</param>
            /// <returns>
            /// The <see cref="T:System.ServiceModel.Channels.Message"/> that is read from the stream specified.
            /// </returns>
            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
            {
                MemoryStream memoryStream = new MemoryStream(buffer.Array, buffer.Offset, buffer.Count - buffer.Offset);
                AMFDeserializer amfDeserializer = new AMFDeserializer(memoryStream);
                AMFMessage amfMessage = amfDeserializer.ReadAMFMessage();
                Message returnMessage = Message.CreateMessage(MessageVersion.None, null);
                returnMessage.Properties.Add("amf", amfMessage);
                return returnMessage;
            }


            /// <summary>
            /// One of the two main entry points into the encoder. Called by WCF to write a message of less than a specified size to a byte array buffer at the specified offset.
            /// </summary>
            /// <param name="message">The <see cref="T:System.ServiceModel.Channels.Message"/> to write to the message buffer.</param>
            /// <param name="maxMessageSize">The maximum message size that can be written.</param>
            /// <param name="bufferManager">The <see cref="T:System.ServiceModel.Channels.BufferManager"/> that manages the buffer to which the message is written.</param>
            /// <param name="messageOffset">The offset of the segment that begins from the start of the byte array that provides the buffer.</param>
            /// <returns>
            /// A <see cref="T:System.ArraySegment`1"/> of type byte that provides the buffer to which the message is serialized.
            /// </returns>
            public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
            {
                MemoryStream memoryStream = new MemoryStream();
                AMFMessage amfMessage = message.Properties["amf"] as AMFMessage;
                AMFSerializer serializer = new AMFSerializer(memoryStream);
                serializer.WriteMessage(amfMessage);
                serializer.Flush();
                // To avoid a buffer copy, we grab a reference to the stream's internal buffer.
                // The byte[] we receive may contain extra nulls after the actual data, due to the
                // buffer management mechanisms of the MemoryStream. Thus, to obtain the message's
                // length, we need to examine memoryStream.Position rather than messageBytes.Length.
                byte[] messageBytes = memoryStream.GetBuffer();
                int messageLength = (int)memoryStream.Position;
                memoryStream.Close();

                int totalLength = messageLength + messageOffset;
                byte[] finalBuffer = bufferManager.TakeBuffer(totalLength);
                Array.Copy(messageBytes, 0, finalBuffer, messageOffset, messageLength);

                ArraySegment<byte> byteArray = new ArraySegment<byte>(finalBuffer, messageOffset, messageLength);
                return byteArray;
            }

            /// <summary>
            /// Called by WCF to decode a buffered byte array into a message.
            /// </summary>
            /// <param name="stream">The <see cref="T:System.IO.Stream"/> object from which the message is read.</param>
            /// <param name="maxSizeOfHeaders">The maximum size of the headers that can be read from the message.</param>
            /// <param name="contentType">The Multipurpose Internet Mail Extensions (MIME) message-level content-type.</param>
            /// <returns>
            /// The <see cref="T:System.ServiceModel.Channels.Message"/> that is read from the stream specified.
            /// </returns>
            public override Message ReadMessage(System.IO.Stream stream, int maxSizeOfHeaders, string contentType)
            {
                /*
                GZipStream gzStream = new GZipStream(stream, CompressionMode.Decompress, true);
                return innerEncoder.ReadMessage(gzStream, maxSizeOfHeaders);
                 */
                return null;
            }

            /// <summary>
            /// When overridden in a derived class, writes a message to a specified stream.
            /// </summary>
            /// <param name="message">The <see cref="T:System.ServiceModel.Channels.Message"/> to write to the <paramref name="stream"/>.</param>
            /// <param name="stream">The <see cref="T:System.IO.Stream"/> object to which the <paramref name="message"/> is written.</param>
            public override void WriteMessage(Message message, System.IO.Stream stream)
            {
                /*
                using (GZipStream gzStream = new GZipStream(stream, CompressionMode.Compress, true))
                {
                    innerEncoder.WriteMessage(message, gzStream);
                }

                // innerEncoder.WriteMessage(message, gzStream) depends on that it can flush data by flushing 
                // the stream passed in, but the implementation of GZipStream.Flush will not flush underlying
                // stream, so we need to flush here.
                stream.Flush();
                 */
            }
        }
    }
}
