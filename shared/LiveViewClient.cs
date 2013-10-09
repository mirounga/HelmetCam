using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HelmetCam
{
    public class LiveViewClient
    {
        private LiveViewClient()
        {
        }

        static public Task LiveViewAsync(Uri endpointUri, CancellationToken cancellationToken, IProgress<byte[]> progress)
        {
            return Task.Run(async () =>
                {
                    HttpClient endPoint = new HttpClient()
                    {
                        Timeout = Timeout.InfiniteTimeSpan
                    };

                    var liveViewStream = await endPoint.GetStreamAsync(endpointUri);

                    byte[] commonHeader = new byte[8];
                    byte[] payloadHeader = new byte[128];

                    while (true)
                    {

                        int cbRead = await FetchBytes(liveViewStream, commonHeader, commonHeader.Length, cancellationToken);

                        cbRead = await FetchBytes(liveViewStream, payloadHeader, payloadHeader.Length, cancellationToken);

                        byte[] payloadSignature = { 0x24, 0x35, 0x68, 0x79 };

                        int payloadIndex = 0;

                        for (; payloadIndex < 4; payloadIndex++)
                        {
                            if (payloadHeader[payloadIndex] != payloadSignature[payloadIndex])
                            {
                                break;
                            }
                        }

                        int jpegSize = payloadHeader[payloadIndex++];
                        jpegSize <<= 8;
                        jpegSize += payloadHeader[payloadIndex++];
                        jpegSize <<= 8;
                        jpegSize += payloadHeader[payloadIndex++];

                        int paddingSize = payloadHeader[payloadIndex++];

                        byte[] jpegBytes = new byte[jpegSize];

                        cbRead = await FetchBytes(liveViewStream, jpegBytes, jpegSize, cancellationToken);

                        progress.Report(jpegBytes);

                        if (paddingSize > 0)
                        {
                            byte[] paddingBytes = new byte[paddingSize];
                            cbRead = await FetchBytes(liveViewStream, paddingBytes, paddingSize, cancellationToken);

                            if (cbRead != paddingBytes.Length)
                            {
                                break;
                            }
                        }
                    }
                });
        }

        static async Task<int> FetchBytes(Stream stream, byte[] buffer, int bytesToRead, CancellationToken cancellationToken)
        {
            for (int bytesRead = 0, bytesRemaining = bytesToRead; bytesRemaining > 0; )
            {
                if (cancellationToken != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                int cbRead = await stream.ReadAsync(buffer, bytesRead, bytesRemaining);

                bytesRead += cbRead;
                bytesRemaining -= cbRead;

                if (bytesRemaining > 0)
                {
                    await Task.Delay(10);
                }
            }

            return bytesToRead;
        }
    }
}
