using System.Windows.Controls;
using ImageGen.Models.Api;
using TextBox = System.Windows.Controls.TextBox;

namespace ImageGen.Helpers;

public static class TextBoxHelper
{
    public static void ApplyTag(TextBox textBox, TagSuggestion selectedTag)
    {
        string text = textBox.Text;
        int caretIndex = textBox.CaretIndex;

        // 커서 위치가 텍스트 범위를 벗어나는 경우 보정
        if (caretIndex > text.Length) caretIndex = text.Length;
        if (caretIndex < 0) caretIndex = 0;

        // 구분자 위치 찾기
        int delimiterIndex = -1;
        if (text.Length > 0)
        {
            delimiterIndex = text.LastIndexOfAny(new[] { ',', '\n' }, Math.Min(Math.Max(0, caretIndex - 1), text.Length - 1));
        }

        int start;
        string prefix = "";
        
        if (delimiterIndex == -1)
        {
            start = 0;
        }
        else
        {
            start = delimiterIndex + 1;
            // 구분자가 쉼표인 경우에만 공백 추가
            if (text[delimiterIndex] == ',')
            {
                prefix = " ";
            }
        }

        // 기존 단어 교체
        string newText = text.Substring(0, start) + prefix + selectedTag.Tag + ", " + text.Substring(caretIndex);
        int newCaretIndex = start + prefix.Length + selectedTag.Tag.Length + 2;
            
        // TextBox의 Text 속성을 직접 변경하고, 바인딩을 업데이트
        textBox.Text = newText;
        var binding = textBox.GetBindingExpression(TextBox.TextProperty);
        binding?.UpdateSource();

        // 커서 위치 복원 및 포커스
        textBox.CaretIndex = newCaretIndex;
        textBox.Focus();
    }
}
