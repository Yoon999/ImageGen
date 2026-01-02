using System.Windows.Media.Imaging;

namespace ImageGen.Services.Interfaces;

public interface IImageService
{
    /// <summary>
    /// 이미지 데이터를 파일로 저장합니다.
    /// </summary>
    Task SaveImageAsync(byte[] imageData, string directoryPath, string fileName);

    /// <summary>
    /// 바이트 배열을 WPF에서 표시 가능한 BitmapImage로 변환합니다.
    /// </summary>
    BitmapImage ConvertToBitmapImage(byte[] imageData);
}
