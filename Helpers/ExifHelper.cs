using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageGen.Helpers;

public static class ExifHelper
{
    public static string ExtractMetadata(string filePath)
    {
        var sb = new StringBuilder();

        try
        {
            using Stream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(fileStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];

            // 1. Try LSB Extraction (NovelAI Stealth PNG)
            try
            {
                string lsbData = ExtractLsbMetadata(frame);
                if (!string.IsNullOrEmpty(lsbData))
                {
                    sb.AppendLine("[Stealth PNG Data]");
                    // 예쁘게 포맷팅
                    try
                    {
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(lsbData);
                        string prettyJson = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
                        sb.AppendLine(prettyJson);
                    }
                    catch
                    {
                        sb.AppendLine(lsbData);
                    }
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                // LSB 추출 실패 시 무시하고 표준 메타데이터 시도
                sb.AppendLine($"[LSB Extraction Failed] {ex.Message}");
            }

            // 2. Try Standard Metadata (tEXt chunks)
            if (frame.Metadata is BitmapMetadata metadata)
            {
                void TryGet(string query, string label)
                {
                    try
                    {
                        if (metadata.ContainsQuery(query))
                        {
                            var value = metadata.GetQuery(query);
                            if (value != null)
                            {
                                sb.AppendLine($"[{label}]");
                                sb.AppendLine(value.ToString());
                                sb.AppendLine();
                            }
                        }
                    }
                    catch { }
                }

                TryGet("/tEXt/{str=Description}", "Description");
                TryGet("/tEXt/{str=Comment}", "Comment");
                TryGet("/tEXt/{str=Software}", "Software");
                TryGet("/tEXt/{str=Source}", "Source");
                TryGet("/tEXt/{str=Title}", "Title");
                TryGet("/tEXt/{str=Author}", "Author");
                TryGet("/tEXt/{str=Copyright}", "Copyright");

                if (!string.IsNullOrEmpty(metadata.Title)) sb.AppendLine($"[Title]\n{metadata.Title}\n");
                if (!string.IsNullOrEmpty(metadata.Comment)) sb.AppendLine($"[Comment]\n{metadata.Comment}\n");
                if (!string.IsNullOrEmpty(metadata.Subject)) sb.AppendLine($"[Subject]\n{metadata.Subject}\n");
            }

            if (sb.Length > 0)
            {
                return sb.ToString();
            }

            return "No metadata found.";
        }
        catch (Exception ex)
        {
            return $"Error extracting metadata: {ex.Message}";
        }
    }

    private static string ExtractLsbMetadata(BitmapSource image)
    {
        // RGBA32 포맷으로 변환
        if (image.Format != PixelFormats.Bgra32)
        {
            image = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        }

        int width = image.PixelWidth;
        int height = image.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        image.CopyPixels(pixels, stride, 0);

        // 알파 채널(A)은 Bgra32에서 4번째 바이트 (B, G, R, A 순서)
        // 하지만 파이썬 코드에서는 [..., -1]로 마지막 채널을 가져옴.
        // Bgra32에서 A는 인덱스 3, 7, 11...
        
        // 알파 채널의 LSB만 추출하여 비트 스트림 생성
        // 파이썬 코드: alpha = np.bitwise_and(alpha, 1) -> packbits
        
        // C#으로 구현:
        // 1. 알파 채널 바이트만 모음
        // 2. 각 바이트의 LSB(1비트)를 추출
        // 3. 8개씩 모아서 바이트로 변환

        // 알파 채널 데이터만 추출 (메모리 최적화를 위해 스트림 방식으로 처리 가능하지만 일단 배열로)
        // Bgra32: B G R A
        
        // 비트들을 모을 리스트 (또는 배열)
        // 예상되는 데이터 크기를 모르므로 일단 넉넉하게 잡거나 동적으로 처리
        // 매직 넘버 "stealth_pngcomp" (15 bytes) 확인을 위해 앞부분만 먼저 처리할 수도 있음.

        var lsbExtractor = new LsbExtractor(pixels);
        
        string magic = "stealth_pngcomp";
        byte[] readMagicBytes = lsbExtractor.GetNextNBytes(magic.Length);
        string readMagic = Encoding.UTF8.GetString(readMagicBytes);

        if (magic != readMagic)
        {
            return null; // 매직 넘버 불일치
        }

        // 데이터 길이 읽기 (32bit integer, Big Endian)
        // 파이썬: read_32bit_integer() // 8 -> 비트 단위 길이를 바이트 단위로 변환
        int bitLength = lsbExtractor.Read32BitInteger();
        int byteLength = bitLength / 8;

        if (byteLength <= 0 || byteLength > pixels.Length) // 유효성 검사
        {
            return null;
        }

        // 데이터 읽기
        byte[] compressedData = lsbExtractor.GetNextNBytes(byteLength);

        // GZIP 압축 해제
        using (var compressedStream = new MemoryStream(compressedData))
        using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
        using (var resultStream = new MemoryStream())
        {
            gzipStream.CopyTo(resultStream);
            return Encoding.UTF8.GetString(resultStream.ToArray());
        }
    }

    private class LsbExtractor
    {
        private readonly byte[] _pixels;
        private int _pixelIndex; // 현재 픽셀 인덱스 (0 ~ totalPixels-1)
        private int _totalPixels;

        public LsbExtractor(byte[] pixels)
        {
            _pixels = pixels;
            _totalPixels = pixels.Length / 4;
            _pixelIndex = 0;
        }

        public byte GetOneByte()
        {
            byte result = 0;
            for (int i = 0; i < 8; i++)
            {
                if (_pixelIndex >= _totalPixels) break;

                // Bgra32에서 Alpha는 3, 7, 11... (index * 4 + 3)
                byte alpha = _pixels[_pixelIndex * 4 + 3];
                int lsb = alpha & 1;
                
                // 파이썬 packbits는 기본적으로 big-endian (MSB first) 처럼 동작하지만,
                // reshape((-1, 8)) 후 packbits(axis=1)은 행 단위로 묶음.
                // 파이썬 코드: alpha = alpha.reshape((-1, 8)); np.packbits(alpha, axis=1)
                // [0, 0, 0, 0, 0, 0, 0, 1] -> 1
                // [1, 0, 0, 0, 0, 0, 0, 0] -> 128
                // 즉, 먼저 읽은 비트가 MSB(Most Significant Bit)가 됨.
                
                result |= (byte)(lsb << (7 - i));
                
                _pixelIndex++;
            }
            return result;
        }

        public byte[] GetNextNBytes(int n)
        {
            byte[] bytes = new byte[n];
            for (int i = 0; i < n; i++)
            {
                bytes[i] = GetOneByte();
            }
            return bytes;
        }

        public int Read32BitInteger()
        {
            byte[] bytes = GetNextNBytes(4);
            if (bytes.Length < 4) return 0;
            
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}
