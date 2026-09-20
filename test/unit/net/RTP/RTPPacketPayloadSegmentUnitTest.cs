//-----------------------------------------------------------------------------
// Filename: RTPPacketPayloadSegmentUnitTest.cs
//
// Description: Unit tests for building an RTP packet from a payload that sits
// partway into a larger buffer.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit;

namespace SIPSorcery.Net.UnitTests
{
    [Trait("Category", "unit")]
    public class RTPPacketPayloadSegmentUnitTest
    {
        private Microsoft.Extensions.Logging.ILogger logger = null;

        public RTPPacketPayloadSegmentUnitTest(Xunit.Abstractions.ITestOutputHelper output)
        {
            logger = SIPSorcery.UnitTests.TestLogHelper.InitTestLogger(output);
        }

        /// <summary>
        /// The public SendRtpRaw overloads taking a segment let a caller send out of a buffer it
        /// owns, such as one rented from an ArrayPool, which is larger than the payload and holds it
        /// partway in. The array overloads always describe the whole array, so an offset was never
        /// exercised from outside the library.
        /// </summary>
        [Fact]
        public void PayloadPartwayIntoABufferIsSentWhole()
        {
            // a rented buffer: bigger than the payload, and with the payload not at the start
            var rented = new byte[512];
            for (int i = 0; i < rented.Length; i++)
            {
                rented[i] = 0xEE; // what must not be sent
            }

            var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            Array.Copy(payload, 0, rented, 100, payload.Length);

            var packet = new RTPPacket(new ArraySegment<byte>(rented, 100, payload.Length), 0);
            packet.Header.PayloadType = 96;
            packet.Header.Timestamp = 1234;

            Assert.Equal((uint)payload.Length, packet.GetPayloadLength());

            byte[] onTheWire = packet.GetBytes();
            int headerLength = packet.Header.GetBytes().Length;

            Assert.Equal(headerLength + payload.Length, onTheWire.Length);
            Assert.Equal(payload, onTheWire.Skip(headerLength).ToArray());

            logger.LogDebug("sent {Length} bytes, payload {Payload}", onTheWire.Length,
                string.Join(",", onTheWire.Skip(headerLength)));
        }

        /// <summary>
        /// The whole array being the payload has to keep working, since that is what every existing
        /// caller does.
        /// </summary>
        [Fact]
        public void AWholeArrayIsStillSentWhole()
        {
            var payload = new byte[] { 9, 8, 7 };

            var packet = new RTPPacket(new ArraySegment<byte>(payload), 0);
            packet.Header.PayloadType = 96;

            byte[] onTheWire = packet.GetBytes();
            int headerLength = packet.Header.GetBytes().Length;

            Assert.Equal(payload, onTheWire.Skip(headerLength).ToArray());
        }
    }
}
