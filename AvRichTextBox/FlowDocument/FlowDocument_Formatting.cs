using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvRichTextBox;

public partial class FlowDocument
{
   internal Dictionary<AvaloniaProperty, FormatRuns> _formatRunsActions = null!;
   internal Dictionary<AvaloniaProperty, FormatRun> _formatRunActions = null!;

   private void DefineFormatRunActions()
   {
      _formatRunsActions = new Dictionary<AvaloniaProperty, FormatRuns>
       {
           { Inline.FontFamilyProperty, ApplyFontFamilyRuns },
           { Inline.FontWeightProperty, ApplyBoldRuns },
           { Inline.FontStyleProperty, ApplyItalicRuns },
           { Inline.TextDecorationsProperty, ApplyTextDecorationRuns },
           { Inline.FontSizeProperty, ApplyFontSizeRuns },
           { Inline.BackgroundProperty, ApplyBackgroundRuns },
           { Inline.ForegroundProperty, ApplyForegroundRuns },
           { Inline.FontStretchProperty, ApplyFontStretchRuns },
           { Inline.BaselineAlignmentProperty, ApplyBaselineAlignmentRuns }
       };


      _formatRunActions = new Dictionary<AvaloniaProperty, FormatRun>
       {
           { Inline.FontFamilyProperty, ApplyFontFamilyRun },
           { Inline.FontWeightProperty, ApplyBoldRun },
           { Inline.FontStyleProperty, ApplyItalicRun },
           { Inline.TextDecorationsProperty, ApplyTextDecorationRun },
           { Inline.FontSizeProperty, ApplyFontSizeRun },
           { Inline.BackgroundProperty, ApplyBackgroundRun },
           { Inline.ForegroundProperty, ApplyForegroundRun },
           { Inline.FontStretchProperty, ApplyFontStretchRun },
           { Inline.BaselineAlignmentProperty, ApplyBaselineAlignmentRun }
       };


   }


   bool _boldOn = false;
   bool _italicOn = false;
   bool _underliningOn = false;

   private bool _insertRunMode = false;
   private ToggleFormatRun? _toggleFormatRun;

   private delegate void ToggleFormatRun(IEditable ied);
   private void ToggleApplyBold(IEditable ied) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontWeight = _boldOn ? FontWeight.Bold : FontWeight.Normal; } }
   private void ToggleApplyItalic(IEditable ied) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontStyle = _italicOn ? FontStyle.Italic : FontStyle.Normal; } }
   private void ToggleApplyUnderline(IEditable ied) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).TextDecorations = _underliningOn ? TextDecorations.Underline : null; } }

   internal void ToggleItalic()
   {
      if (Selection.Length == 0)
      {
         
         _italicOn = !_italicOn;
         _toggleFormatRun = ToggleApplyItalic;
         _insertRunMode = true;
         IEditable startInline = Selection.GetStartInline();
         if (startInline != Selection.StartParagraph.Inlines.Last() && startInline.GetCharPosInInline(Selection.Start) == startInline.InlineText.Length)
         {
            IEditable nextInline = Selection.StartParagraph.Inlines[Selection.StartParagraph.Inlines.IndexOf(startInline) + 1];
            bool nextRunItalic = nextInline.GetType() == typeof(EditableRun) && ((EditableRun)nextInline).FontStyle == FontStyle.Italic;
            _insertRunMode = (_italicOn != nextRunItalic);
            Selection.BiasForwardStart = !_insertRunMode;
         }
      }
      else
         Selection.ApplyFormatting(Inline.FontStyleProperty, FontStyle.Italic);

   }

   internal void ToggleBold()
   {
      if (Selection.Length == 0)
      {
         _toggleFormatRun = ToggleApplyBold;
         _boldOn = !_boldOn;
         _insertRunMode = true;
         IEditable startInline = Selection.GetStartInline();

         if (startInline != Selection.StartParagraph.Inlines.Last() && startInline.GetCharPosInInline(Selection.Start) == startInline.InlineText.Length)
         {
            IEditable nextInline = Selection.StartParagraph.Inlines[Selection.StartParagraph.Inlines.IndexOf(startInline) + 1];
            bool nextRunBold = nextInline.GetType() == typeof(EditableRun) && ((EditableRun)nextInline).FontWeight == FontWeight.Bold;
            _insertRunMode = (_boldOn != nextRunBold);
            Selection.BiasForwardStart = !_insertRunMode;
         }
      }
      else
         Selection.ApplyFormatting(Inline.FontWeightProperty, FontWeight.Bold);

   }

   internal void ToggleUnderlining()
   {
      if (Selection.Length == 0)
      {
         _toggleFormatRun = ToggleApplyUnderline;
         _underliningOn = !_underliningOn;
         _insertRunMode = true;

         IEditable startInline = Selection.GetStartInline();

         if (startInline != Selection.StartParagraph.Inlines.Last() && startInline.GetCharPosInInline(Selection.Start) == startInline.InlineText.Length)
         {
            IEditable nextInline = Selection.StartParagraph.Inlines[Selection.StartParagraph.Inlines.IndexOf(startInline) + 1];
            bool nextRunUnderlined = nextInline.GetType() == typeof(EditableRun) && ((EditableRun)nextInline).TextDecorations == TextDecorations.Underline;
            _insertRunMode = (_underliningOn != nextRunUnderlined);
            Selection.BiasForwardStart = !_insertRunMode;
         }
      }
      else
         Selection.ApplyFormatting(Inline.TextDecorationsProperty, TextDecorations.Underline);
   }

   
   internal void ApplyFormattingInline(FormatRun formatRun, IEditable inlineItem, object value)
   {
      formatRun(inlineItem, value);
      Selection.BiasForwardStart = true;
      Selection.BiasForwardEnd = true;

   }

   internal delegate void FormatRun(IEditable ied, object value);
   private void ApplyFontFamilyRun(IEditable ied, object fontfamily ) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontFamily = (FontFamily)fontfamily; } }
   private void ApplyBoldRun(IEditable ied, object fontWeight) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontWeight = (FontWeight)fontWeight; } }
   private void ApplyItalicRun(IEditable ied, object fontStyle) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontStyle = (FontStyle)fontStyle; } }
   private void ApplyTextDecorationRun(IEditable ied, object textDecoration) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).TextDecorations = (TextDecorationCollection)textDecoration; } }
   private void ApplyFontSizeRun(IEditable ied, object fontsize) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontSize = (double)fontsize; } }
   private void ApplyBackgroundRun(IEditable ied, object background) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).Background = (SolidColorBrush)background; } }
   private void ApplyForegroundRun(IEditable ied, object foreground) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).Foreground = (SolidColorBrush)foreground; } }
   private void ApplyFontStretchRun(IEditable ied, object fontstretch) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontStretch = (FontStretch)fontstretch; } }
   private void ApplyBaselineAlignmentRun(IEditable ied, object baselinealignment) { if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).BaselineAlignment = (BaselineAlignment)baselinealignment ; } }


   internal delegate void FormatRuns(List<IEditable> ieds, object value);
   
   private void ApplyFontFamilyRuns(List<IEditable> ieds, object fontfamily)
   {
      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontFamily = (FontFamily)fontfamily; }
   }
      
   private void ApplyBoldRuns(List<IEditable> ieds, object fontweight)
   {
      FontWeight applyFontWeight = FontWeight.Normal;
      if (fontweight is FontWeight.Bold)
         applyFontWeight = (ieds.Where(ar => ar.GetType() == typeof(EditableRun) && ((EditableRun)ar).FontWeight == FontWeight.Normal).Count() == 0) ?
            FontWeight.Normal : FontWeight.Bold;
      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontWeight = applyFontWeight; }
   }

   private void ApplyItalicRuns(List<IEditable> ieds, object fontstyle)
   {
      FontStyle applyFontStyle = FontStyle.Normal;
      if (fontstyle is FontStyle.Italic)
         applyFontStyle = (ieds.Where(ar => ar.GetType() == typeof(EditableRun) && ((EditableRun)ar).FontStyle == FontStyle.Normal).Count() == 0) ? FontStyle.Normal : FontStyle.Italic;
      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontStyle = applyFontStyle; }
   }

   private void ApplyTextDecorationRuns(List<IEditable> ieds, object textdecoration)
   {
      TextDecorationCollection applyTextDecs = null!;
      if (textdecoration == TextDecorations.Underline)
         applyTextDecs = (ieds.Where(ar => ar.GetType() == typeof(EditableRun) && ((EditableRun)ar).TextDecorations == null).Count() == 0) ? null! : TextDecorations.Underline;
      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).TextDecorations = applyTextDecs; }
   }
   
   private void ApplyFontSizeRuns(List<IEditable> ieds, object fontsize)
   {
      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontSize = (double)fontsize; }
   }
      
   private void ApplyBackgroundRuns(List<IEditable> ieds, object background)
   {
      if (background.GetType() != typeof(SolidColorBrush))
         throw new Exception("Background must be set with a SolidColorBrush");

      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).Background = (SolidColorBrush)background; }
   }

   private void ApplyForegroundRuns(List<IEditable> ieds, object foreground)
   {
      if (foreground.GetType() != typeof(SolidColorBrush))
         throw new Exception("Foreground must be set with a SolidColorBrush");

      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).Foreground = (SolidColorBrush)foreground; }
   }

   private void ApplyFontStretchRuns(List<IEditable> ieds, object fontstretch)
   {
      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).FontStretch = (FontStretch)fontstretch; }
   }

   private void ApplyBaselineAlignmentRuns(List<IEditable> ieds, object baselinealignment)
   {
      foreach (IEditable ied in ieds)
         if (ied.GetType() == typeof(EditableRun)) { ((EditableRun)ied).BaselineAlignment = (BaselineAlignment)baselinealignment; }
   }

   internal void ResetInsertFormatting()
   {
      _insertRunMode = false;
      _boldOn = false;
      _italicOn = false;
      _underliningOn = false;

   }
}

