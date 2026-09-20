//-----------------------------------------------------------------------------
// Filename: RTCPSenderReportTimestampPairUnitTest.cs
//
// Description: Unit tests for the NTP and RTP timestamp pair in the sender
// reports an RTCPSession generates.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Xunit;

namespace SIPSorcery.Net.UnitTests
{
    [Trait("Category", "unit")]
    public class RTCPSenderReportTimestampPairUnitTest
    {
        private Microsoft.Extensions.Logging.ILogger logger = null;

        public RTCPSenderReportTimestampPairUnitTest(Xunit.Abstractions.ITestOutputHelper output)
        {
            logger = SIPSorcery.UnitTests.TestLogHelper.InitTestLogger(output);
        }

        private static RTPPacket PacketWithTimestamp(uint timestamp, ushort seqNum)
        {
            var packet = new RTPPacket(160);
            packet.Header.Timestamp = timestamp;
            packet.Header.SequenceNumber = seqNum;
            return packet;
        }

        /// <summary>
        /// RFC 3550 6.4.1: the NTP and RTP timestamps in a sender report are two readings of the
        /// same instant. A receiver uses them to place a stream's RTP clock on a wall clock, which
        /// is the only thing that lets it line two streams of a session up against each other.
        /// </summary>
        /// <remarks>
        /// The report used to pair the RTP timestamp of the last packet sent with the wall clock
        /// reading taken when the report was built, which are the same instant only if a packet
        /// happened to go out as the report was made. Everything in between was error, and it
        /// differed per stream - video goes out a frame at a time and audio every 20ms - so the two
        /// streams of a session were placed on the wall clock with different errors and drifted
        /// apart by the difference.
        /// </remarks>
        [Fact]
        public void SenderReportPairsTheRtpTimestampWithWhenItWasSent()
        {
            var session = new RTCPSession(SDPMediaTypesEnum.audio, 1234);

            ulong beforeSend = RTCPSession.DateTimeToNtpTimestamp(DateTime.Now);
            session.RecordRtpPacketSend(PacketWithTimestamp(90000, 1));
            ulong afterSend = RTCPSession.DateTimeToNtpTimestamp(DateTime.Now);

            // the gap between the last packet and the report being built, which is where a report
            //  interval's worth of error used to come from
            Thread.Sleep(300);

            var report = session.GetRtcpReport();
            ulong reportBuiltAt = RTCPSession.DateTimeToNtpTimestamp(DateTime.Now);

            Assert.NotNull(report.SenderReport);
            Assert.Equal(90000u, report.SenderReport.RtpTimestamp);

            logger.LogDebug("send {Before}-{After}, report says {Ntp}, built at {Built}",
                beforeSend, afterSend, report.SenderReport.NtpTimestamp, reportBuiltAt);

            Assert.True(report.SenderReport.NtpTimestamp >= beforeSend,
                "the reported time is before the packet was sent");
            Assert.True(report.SenderReport.NtpTimestamp <= afterSend,
                "the reported time is after the packet was sent - it is not the instant the RTP timestamp belongs to");
        }

        /// <summary>
        /// Two streams reported at the same moment, having last sent packets at different times,
        /// must not be placed on the wall clock relative to one another by when the reports happened
        /// to be built.
        /// </summary>
        [Fact]
        public void TwoStreamsAreEachPairedWithTheirOwnSendTime()
        {
            var video = new RTCPSession(SDPMediaTypesEnum.video, 1);
            var audio = new RTCPSession(SDPMediaTypesEnum.audio, 2);

            // a video frame goes out, then 200ms later an audio packet does
            video.RecordRtpPacketSend(PacketWithTimestamp(90000, 1));
            Thread.Sleep(200);
            audio.RecordRtpPacketSend(PacketWithTimestamp(48000, 1));

            // both reports are built now, well after either packet went out
            Thread.Sleep(100);
            var videoReport = video.GetRtcpReport();
            var audioReport = audio.GetRtcpReport();

            ulong gap = audioReport.SenderReport.NtpTimestamp - videoReport.SenderReport.NtpTimestamp;
            double gapSeconds = gap / (double)0x100000000L;

            logger.LogDebug("gap between the two reported times: {GapSeconds}s", gapSeconds);

            // the reports say the streams' last packets were ~200ms apart, because they were
            Assert.True(gapSeconds > 0.15 && gapSeconds < 0.30,
                $"the two streams were placed {gapSeconds}s apart, expected about 0.2s");
        }
    }
}
