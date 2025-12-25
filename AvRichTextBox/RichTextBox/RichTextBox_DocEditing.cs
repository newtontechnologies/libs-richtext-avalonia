using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.Text.RegularExpressions;

namespace AvRichTextBox;

public partial class RichTextBox
{

   private void RichTextBox_TextInput(object? sender, Avalonia.Input.TextInputEventArgs e)
   {
      if (IsReadOnly) return;

      FlowDoc.InsertText(e.Text);
      UpdateCurrentParagraphLayout();
      
      if (PreeditOverlay.IsVisible)
         HideImeOverlay();
         
   }

   private void HideImeOverlay()
   {
      _preeditText = "";
      PreeditOverlay.IsVisible = false;

   }

   internal void UpdateCurrentParagraphLayout()
   {
      this.UpdateLayout();
      RtbVm.UpdateCaretVisible();

      // Caret position (CaretMargin/CaretHeight) is computed in code-behind handlers
      // `SelectionStart_RectChanged` / `SelectionEnd_RectChanged`, which are triggered by
      // Paragraph.RequestTextLayoutInfoStart/End toggles. During coalesced typing we may do
      // an internal undo+redo in one input event; forcing a layout info refresh here keeps
      // the visual caret in sync with the (correct) selection indices.
      FlowDoc.Selection.StartParagraph.CallRequestTextLayoutInfoStart();
      FlowDoc.Selection.EndParagraph.CallRequestTextLayoutInfoEnd();
   }

   internal void InsertParagraph()
   {
      if (IsReadOnly) return;

      FlowDoc.ExecuteEdit(FlowDoc.BuildReplaceRangeAction(FlowDoc.Selection.Start, FlowDoc.Selection.End, [new EditableRun("\r")]));
      UpdateCurrentParagraphLayout();

   }

   internal void InsertLineBreak()
   {
      if (IsReadOnly) return;

      FlowDoc.InsertLineBreak();
      UpdateCurrentParagraphLayout();

   }

   public void SearchText(string searchText)
   {        
      MatchCollection matches = Regex.Matches(FlowDoc.Text, searchText);

      if (matches.Count > 0)
         FlowDoc.Select(matches[0].Index, matches[0].Length);

      
      foreach (Match m in matches)
      {
         TextRange trange = new (FlowDoc, m.Index, m.Index + m.Length);
         FlowDoc.ApplyFormattingRange(Inline.FontStretchProperty, FontStretch.UltraCondensed, trange);
         FlowDoc.ApplyFormattingRange(Inline.ForegroundProperty, new SolidColorBrush(Colors.BlueViolet), trange);
         FlowDoc.ApplyFormattingRange(Inline.BackgroundProperty, new SolidColorBrush(Colors.Wheat), trange);
      }
         


   }


   private void PerformDelete(bool backspace)
   {
      if (IsReadOnly) return;

      if (FlowDoc.Selection!.Length > 0)
         FlowDoc.DeleteSelection();
      else
      {
         if (backspace)
            if (FlowDoc.Selection.Start == 0) return;
         else
            if (FlowDoc.Selection.Start >= FlowDoc.Selection.StartParagraph.StartInDoc + FlowDoc.Selection.StartParagraph.BlockLength)
               return;

         FlowDoc.DeleteChar(backspace);
      }

      UpdateCurrentParagraphLayout();
   }

   
}
