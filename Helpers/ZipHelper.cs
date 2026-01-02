using System.IO;
using System.IO.Compression;

namespace ImageGen.Helpers;

public static class ZipHelper
{
    /// <summary>
    /// ZIP 바이너리 데이터에서 첫 번째 이미지 파일(PNG)을 추출합니다.
    /// </summary>
    /// <param name="zipData">ZIP 파일의 바이트 배열</param>
    /// <returns>추출된 이미지 파일의 바이트 배열. 실패 시 null 반환.</returns>
    public static byte[]? ExtractFirstImage(byte[] zipData)
    {
        try
        {
            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            // ZIP 내의 첫 번째 파일(보통 이미지)을 찾음
            var entry = archive.Entries.FirstOrDefault();
            if (entry == null) return null;

            using var entryStream = entry.Open();
            using var outputStream = new MemoryStream();
            
            entryStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }
        catch
        {
            // 압축 해제 실패 시 null 반환 또는 예외 처리
            return null;
        }
    }
}
