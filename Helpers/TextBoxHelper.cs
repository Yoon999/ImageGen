using System.Text.RegularExpressions;
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

    public static void AdjustWeight(TextBox textBox, double delta)
    {
        string text = textBox.Text;
        int caretIndex = textBox.CaretIndex;

        // Find word boundaries (comma or newline)
        // Search backwards for start
        int start = -1;
        if (caretIndex > 0)
        {
            start = text.LastIndexOfAny(new[] { ',', '\n' }, caretIndex - 1);
        }
        
        if (start == -1) start = 0;
        else start++; // Skip the delimiter

        // Search forwards for end
        int end = text.IndexOfAny(new[] { ',', '\n' }, caretIndex);
        if (end == -1) end = text.Length;

        if (start >= end) return;

        string originalSegment = text.Substring(start, end - start);
        string currentWord = originalSegment.Trim();
        
        if (string.IsNullOrEmpty(currentWord)) return;

        // Check format: weight::text::
        // Regex to capture weight and content. 
        // Handles cases like "1.2::heart::" -> weight=1.2, content="heart"
        // Also handles simple text "heart" -> weight=1.0 (default), content="heart"
        // Updated regex to support negative numbers: ^(-?\d+(?:\.\d+)?)::(.+)::$
        var match = Regex.Match(currentWord, @"^(-?\d+(?:\.\d+)?)::(.+)::$");
        
        double weight = 1.0;
        string content = currentWord;

        if (match.Success)
        {
            if (double.TryParse(match.Groups[1].Value, out double w))
            {
                weight = w;
                content = match.Groups[2].Value;
            }
        }
        else
        {
            // If it doesn't match the pattern, assume weight is 1.0 and the whole word is content
            weight = 1.0;
            content = currentWord;
        }

        // Adjust weight
        weight = Math.Round(weight + delta, 1);
        // Removed the check for weight < 0 to allow negative weights
        
        // Construct new word
        string newWord;
        if (Math.Abs(weight - 1.0) < 0.001)
        {
            newWord = content;
        }
        else
        {
            newWord = $"{weight:0.0}::{content}::";
        }

        // Preserve whitespace around the word in the segment
        int wordStartInSegment = originalSegment.IndexOf(currentWord);
        string leading = "";
        string trailing = "";
        
        if (wordStartInSegment >= 0)
        {
            leading = originalSegment.Substring(0, wordStartInSegment);
            // The rest after the word
            if (wordStartInSegment + currentWord.Length < originalSegment.Length)
            {
                trailing = originalSegment.Substring(wordStartInSegment + currentWord.Length);
            }
        }

        string replacement = leading + newWord + trailing;
        
        // Replace in text
        string newText = text.Remove(start, end - start).Insert(start, replacement);
        
        textBox.Text = newText;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        
        // Restore caret. 
        textBox.CaretIndex = start + leading.Length + newWord.Length;
    }
}
