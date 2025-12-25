using Avalonia;
using Avalonia.Input;
using DocumentFormat.OpenXml.Drawing.Charts;
using System;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AvRichTextBox;

public class TextRange : INotifyPropertyChanged, IDisposable
{
   public event PropertyChangedEventHandler? PropertyChanged;
   private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

   internal delegate void StartChangedHandler(TextRange sender, int newStart);
   internal event StartChangedHandler? StartChanged;
   internal delegate void EndChangedHandler(TextRange sender, int newEnd);
   internal event EndChangedHandler? EndChanged;

   public override string ToString() => this.Start + " → " + this.End;

   public TextRange(FlowDocument flowdoc, int start, int end)
   {
      if (end < start) throw new AvaloniaInternalException("TextRange not valid (start must be less than end)");

      this.Start = start;
      this.End = end;
      MyFlowDoc = flowdoc;
      MyFlowDoc.TextRanges.Add(this);

   }

   internal FlowDocument MyFlowDoc;
   public int Length  => End - Start;
   private int _start;
   public int Start { get => _start; set { if (_start != value) { _start = value;  StartChanged?.Invoke(this, value); NotifyPropertyChanged(nameof(Start)); } } }
      
   private int _end;
   public int End { get => _end; set { if (_end != value) { _end = value; EndChanged?.Invoke(this, value); NotifyPropertyChanged(nameof(End)); } } }

   internal void UpdateStart() { NotifyPropertyChanged(nameof(Start)); }
   internal void UpdateEnd() { NotifyPropertyChanged(nameof(End)); }

   internal Paragraph StartParagraph = null!;
   internal Paragraph EndParagraph = null!;

   internal Rect PrevCharRect;
   internal Rect StartRect { get; set; }
   internal Rect EndRect { get; set; }
   internal bool IsAtEndOfLineSpace = false;
   internal bool IsAtEndOfLine = false;
   internal bool IsAtLineBreak = false;

   internal bool BiasForwardStart = true;
   internal bool BiasForwardEnd = true;
   public void CollapseToStart() { End = Start;  }
   public void CollapseToEnd() { Start = End ; }


   internal IEditable GetStartInline()
   {
      
      Paragraph? startPar = GetStartPar();
      if (startPar == null) return null!;
      IEditable startInline = null!;
      IsAtLineBreak = false;

      if (BiasForwardStart)
      {
         IEditable startInlineReal = startPar.Inlines.LastOrDefault(ied => startPar.StartInDoc + ied.TextPositionOfInlineInParagraph <= Start)!;
         startInline = startPar.Inlines.LastOrDefault(ied => !ied.IsLineBreak && startPar.StartInDoc + ied.TextPositionOfInlineInParagraph <= Start)!;
         IsAtLineBreak = startInline != startInlineReal;
         //Debug.WriteLine("calculating isatlinebreak biasforwardstart");
      }
      else
      {
         if (Start - startPar.StartInDoc == 0)
            startInline = startPar.Inlines.FirstOrDefault()!;
         else
         {
            //Debug.WriteLine("calculating isatlinebreak - OTHER");
            startInline = startPar.Inlines.LastOrDefault(ied => startPar.StartInDoc + ied.TextPositionOfInlineInParagraph < Start)!;
            IEditable startInlineUpToLineBreak = startPar.Inlines.LastOrDefault(ied => !ied.IsLineBreak && startPar.StartInDoc + ied.TextPositionOfInlineInParagraph < Start)!;
            if (startInline.IsLineBreak)
               startInline = MyFlowDoc.GetNextInline(startInline) ?? startInline;
            IsAtLineBreak = startInline != startInlineUpToLineBreak;
         }
      }

      return startInline!;

   }


   internal IEditable GetEndInline()
   {
      Paragraph? endPar = GetEndPar();
      if (endPar == null) return null!;

      IEditable endInline = null!;

      //if (trange.BiasForwardStart && trange.Length == 0)
      if (BiasForwardStart)
         endInline = endPar.Inlines.LastOrDefault(ied => endPar.StartInDoc + ied.TextPositionOfInlineInParagraph <= End)!;
      else
         endInline = endPar.Inlines.LastOrDefault(ied => endPar.StartInDoc + ied.TextPositionOfInlineInParagraph < End)!;

      return endInline!;
   }


   public Paragraph? GetStartPar()
   {
      Paragraph? startPar = MyFlowDoc.Blocks.LastOrDefault(b => b.IsParagraph && (b.StartInDoc <= Start))! as Paragraph;

      if (startPar != null)
      {
         //Check if start at end of last paragraph (cannot span from end of a paragraph)
         if (startPar != MyFlowDoc.Blocks.Where(b => b.IsParagraph).Last() && startPar!.EndInDoc == Start)
            startPar = MyFlowDoc.Blocks.FirstOrDefault(b => b.IsParagraph && MyFlowDoc.Blocks.IndexOf(b) > MyFlowDoc.Blocks.IndexOf(startPar))! as Paragraph;
      }

      return startPar;

   }

   public Paragraph? GetEndPar()
   {
      return MyFlowDoc.Blocks.LastOrDefault(b => b.IsParagraph && b.StartInDoc < End)! as Paragraph;  // less than to keep within emd of paragraph

   }

   public object? GetFormatting(AvaloniaProperty avProp)
   {
      object? formatting = null!;
      if (MyFlowDoc == null) return null!;
      IEditable currentInline = GetStartInline();
      if (currentInline != null)
         formatting = MyFlowDoc.GetFormattingInline(avProp, currentInline);
      
      return formatting;
   }

   bool _isFormatting = false;
   public void ApplyFormatting(AvaloniaProperty avProp, object value)
   {
      if (MyFlowDoc == null) return;
      if (Length < 1) return;

      //try
      //{
      //   Debug.WriteLine("\napplying: " + (this.Text ?? "null"));
      //}
      //catch (Exception ex) { Debug.WriteLine("exception, length = " + this.Length + " :::start = " + this.Start + " ::: end= " + this.End + " :::"  + ex.Message); }
      if (this.Text == "") return;

      MyFlowDoc.ApplyFormattingRange(avProp, value, this);

      BiasForwardStart = false;
      BiasForwardEnd = false;
      
   }

   internal string GetText()
   {
      if (MyFlowDoc == null) return "";
      return MyFlowDoc.GetText(this);
   }
 
   public string Text
   {
      get => GetText();
      set => MyFlowDoc.SetRangeToText(this, value);
   }

   public void Dispose()
   {    
      Dispose(true);
      GC.SuppressFinalize(this);
    
   }

   private bool _disposed = false;
   protected virtual void Dispose(bool disposing)
   {
      if (_disposed)
         return;

      if (disposing)
      {
         StartParagraph = null!;
         EndParagraph = null!;

         StartChanged = null;
         EndChanged = null;
         this.Start = 0; this.End = 0;
         MyFlowDoc.TextRanges.Remove(this);
         MyFlowDoc = null!;

      }
      _disposed = true;
   }



}

