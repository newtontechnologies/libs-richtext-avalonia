using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Core;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DynamicData;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace AvRichTextBox;

public partial class RichTextBox
{

   EditableParagraph? _currentMouseOverEp = null;

   internal void EditableParagraph_MouseMove(EditableParagraph edPar, int charIndex)
   {
      if (!_pointerDownOverRtb)
         _currentMouseOverEp = edPar;

   }


   private void EditableParagraph_LostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
   {
      this.Focus();
   }


   internal int SelectionOrigin = 0;
   bool _pointerDownOverRtb = false;

   private void FlowDocSV_PointerPressed(object? sender, PointerPressedEventArgs e)
   {
      if (_currentMouseOverEp == null) return;

      _pointerDownOverRtb = true;

      TextHitTestResult hitCarIndex = _currentMouseOverEp.TextLayout.HitTestPoint(e.GetPosition(_currentMouseOverEp));
      Paragraph thisPar = (Paragraph)_currentMouseOverEp.DataContext!;
      if (thisPar == null) return;
      SelectionOrigin = thisPar.StartInDoc + hitCarIndex.TextPosition;

      //Clear all selections in all paragraphs      
      foreach (Paragraph p in FlowDoc.Blocks.Where(pp => pp.SelectionLength != 0)) { p.ClearSelection();  }

      int selStartIdx = SelectionOrigin;
      int selEndIdx = SelectionOrigin;

      if(e.ClickCount > 1 &&
         e.Source is Visual sourceVisual &&
         e.GetCurrentPoint(sourceVisual).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) 
      { // word/paragraph selection added by tkefauver
         if(e.ClickCount == 2) 
         {
            // dbl click, select word
            var wordMatches = Regex.Matches(thisPar.Text, "\\w+");
            foreach(Match wm in wordMatches) 
            {
               int wmStartIdx = thisPar.StartInDoc + wm.Index;
               int wmEndIdx = wmStartIdx + wm.Length;
               if(SelectionOrigin >= wmStartIdx && SelectionOrigin <= wmEndIdx) 
               {
                  selStartIdx = wmStartIdx;
                  selEndIdx = wmEndIdx;
                  break;
               }
            }
         } 
         else if(e.ClickCount == 3) 
         {
            // triple click select block
            selStartIdx = thisPar.StartInDoc;
            selEndIdx = selStartIdx + thisPar.Text.Length;
         } 
      }

      FlowDoc.Selection.Start = selStartIdx;
      FlowDoc.Selection.End = selEndIdx;

      //e.Pointer.Capture(null);
      //e.Pointer.Capture(this);

   }

   private void FlowDocSV_PointerMoved(object? sender, PointerEventArgs e)
   {      

      if (_pointerDownOverRtb)
      {
         EditableParagraph overEp = null!;

         double rtbTransformedY = this.GetTransformedBounds()!.Value.Clip.Y;

         foreach (KeyValuePair<EditableParagraph, Rect> kvp in VisualHelper.GetVisibleEditableParagraphs(FlowDocSV))
         {  //Debug.WriteLine("visiPar = " + kvp.Key.Text);

            Point ePoint = e.GetCurrentPoint(FlowDocSV).Position;
            Rect thisEpRect = new(kvp.Value.X - DocIC.Margin.Left, kvp.Value.Y, kvp.Value.Width, kvp.Value.Height);

            double adjustedMouseY = ePoint.Y + rtbTransformedY;
            bool epContainsPoint = thisEpRect.Top <= adjustedMouseY && thisEpRect.Bottom >= adjustedMouseY;
            
            if (epContainsPoint)
               { overEp = kvp.Key; break; }
         }

         if (overEp != null)
         {

            TextHitTestResult hitCharIndex = overEp.TextLayout.HitTestPoint(e.GetPosition(overEp));
            int charIndex = hitCharIndex.TextPosition;

            Paragraph thisPar = (Paragraph)overEp.DataContext!;
         
            if (thisPar.StartInDoc + charIndex < SelectionOrigin)
            {  //Debug.WriteLine("startindoc = " + thisPar.StartInDoc + " :::charindex = " +  charIndex + " :::selectionorigin= " + SelectionOrigin);
               FlowDoc.SelectionExtendMode = FlowDocument.ExtendMode.ExtendModeLeft;
               FlowDoc.Selection.End = SelectionOrigin;
               FlowDoc.Selection.Start = thisPar.StartInDoc + charIndex;
            }
            else
            {
               FlowDoc.SelectionExtendMode = FlowDocument.ExtendMode.ExtendModeRight;
               FlowDoc.Selection.Start = SelectionOrigin;
               FlowDoc.Selection.End = thisPar.StartInDoc + charIndex;
            }

            FlowDoc.EnsureSelectionContinuity();
         }
      }

   }

   private void RichTextBox_PointerReleased(object? sender, PointerReleasedEventArgs e)
   {
      _pointerDownOverRtb = false;
      
   }

   private void FlowDocSV_PointerReleased(object? sender, PointerReleasedEventArgs e)
   {
      //e.Pointer.Capture(null);
      _pointerDownOverRtb = false;

   }

   private void RichTextBox_PointerExited(object? sender, PointerEventArgs e)
   {
      //PointerDownOverRTB = false;

   }

}


