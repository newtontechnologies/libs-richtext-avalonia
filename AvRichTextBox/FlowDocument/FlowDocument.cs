using Avalonia;
using DynamicData;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AvRichTextBox;

public partial class FlowDocument : AvaloniaObject, INotifyPropertyChanged
{
   public new event PropertyChangedEventHandler? PropertyChanged;
   private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

   public delegate void ScrollInDirectionHandler(int direction);
   internal event ScrollInDirectionHandler? ScrollInDirection;

   public delegate void SelectionChangedHandler(TextRange selection);
   public event SelectionChangedHandler? SelectionChanged;

   public delegate void UpdateRtbCaretHandler();
   internal event UpdateRtbCaretHandler? UpdateRtbCaret;
   
   private Thickness _pagePadding = new (0);
   public Thickness PagePadding { get => _pagePadding; set {  _pagePadding = value; NotifyPropertyChanged(nameof(PagePadding)); } }

   internal bool IsEditable { get; set; } = true;

   internal EditActionStack UndoStack { get; } = new();
   internal EditActionStack RedoStack { get; } = new();
   internal bool CanUndo => UndoStack.Any;
   internal bool CanRedo => RedoStack.Any;
   internal int UndoCount => UndoStack.Count;
   internal int RedoCount => RedoStack.Count;

   internal List<TextRange> TextRanges = [];

   public ObservableCollection<Block> Blocks { get; set; } = [];

   //public static readonly StyledProperty<ObservableCollection<Block>> BlocksProperty =
   //AvaloniaProperty.Register<FlowDocument, ObservableCollection<Block>>(nameof(Blocks), [], defaultBindingMode: BindingMode.TwoWay);

   //public ObservableCollection<Block> Blocks
   //{
   //   get => GetValue(BlocksProperty);
   //   set { SetValue(BlocksProperty, value); }
   //}

   public string Text => string.Join("", Blocks.ToList().ConvertAll(b => string.Join("", b.Text + "\r")));
   
   public int DocEndPoint => ((Paragraph)Blocks.Last()).EndInDoc;

   public TextRange Selection { get; set; }

   public void SelectAll()
   {
      Selection.Start = 0;
      Selection.End = 0;
      SelectionParagraphs.Clear();
      Selection.End = this.DocEndPoint - 1;
      EnsureSelectionContinuity();
      this.SelectionExtendMode = ExtendMode.ExtendModeRight;
   }

   public void Select(int start, int length)
   {
      SelectionParagraphs.Clear();

      Selection.Start = start;
      Selection.End = start + length;

      EnsureSelectionContinuity();

      UpdateSelection();

   }

   internal void UpdateSelection()
   {
      UpdateBlockAndInlineStarts(Selection.StartParagraph);

      Selection.StartParagraph.CallRequestInlinesUpdate();
      Selection.GetStartInline();
      Selection.StartParagraph.CallRequestTextLayoutInfoStart();

      Selection.EndParagraph.CallRequestInlinesUpdate();
      Selection.GetEndInline();
      Selection.EndParagraph.CallRequestTextLayoutInfoEnd();

      //Selection.StartParagraph.CallRequestTextBoxFocus();
   }

   public FlowDocument()
   {

      Selection = new TextRange(this, 0, 0);
      Selection.StartChanged += SelectionStart_Changed;
      Selection.EndChanged += SelectionEnd_Changed;

      NewDocument();

      DefineFormatRunActions();
   }

   internal void NewDocument()
   {
      ClearDocument();

      Paragraph newpar = new();
      EditableRun newerun = new("");
      newpar.Inlines.Add(newerun);
      Blocks.Add(newpar);

      InitializeDocument();

   }

   internal void ClearDocument()
   {
      Blocks.Clear();
      
      for (int tRangeNo = TextRanges.Count - 1; tRangeNo >= 0; tRangeNo--)
      {
         if (!TextRanges[tRangeNo].Equals(Selection))
            TextRanges[tRangeNo].Dispose();
      }

      this.PagePadding = new Thickness(0);

      UndoStack.Clear();
      RedoStack.Clear();

   }


   internal void InitializeDocument()
   {
      Selection.Start = 0;  //necessary
      Selection.CollapseToStart();

      InitializeParagraphs();

      UpdateRtbCaret?.Invoke();


   }

   internal async void InitializeParagraphs()
   {
      UpdateBlockAndInlineStarts(0);

      Selection.BiasForwardStart = true;
      Selection.BiasForwardEnd = true;
      SelectionExtendMode = ExtendMode.ExtendModeNone;
      SelectionStart_Changed(Selection, 0);
      SelectionEnd_Changed(Selection, 0);

      Selection.UpdateStart();
      Selection.UpdateEnd();

      await Task.Delay(70);  // For caret

      Paragraph firstPar = (Paragraph)Blocks[0];
      firstPar.CallRequestTextBoxFocus();
      firstPar.CallRequestTextLayoutInfoStart();
      firstPar.CallRequestTextLayoutInfoEnd();

   }

   internal void SelectionStart_Changed(TextRange selRange, int newStart)
   {

      Paragraph startPar = GetContainingParagraph(newStart);
      selRange.StartParagraph = startPar;
      startPar.SelectionStartInBlock = newStart - startPar.StartInDoc;
      startPar.CallRequestTextLayoutInfoStart();
      IEditable startInline = selRange.GetStartInline();

      UpdateSelectedParagraphs();

      if (ShowDebugger)
         UpdateDebuggerSelectionParagraphs();

      //Make sure end is not less than start
      if (Selection.Length > 0)
         if (selRange.StartParagraph.SelectionEndInBlock < selRange.StartParagraph.SelectionStartInBlock)
            selRange.StartParagraph.SelectionEndInBlock = selRange.StartParagraph.SelectionStartInBlock;

      //if (selRange.StartParagraph != null)
      //   selRange.StartParagraph.CallRequestTextLayoutInfoStart();

      //Debug.WriteLine("startpar text? = " + selRange.StartParagraph?.Text + "\n________________");

      //Selection.StartParagraph.CallRequestInlinesUpdate();
      Selection.GetStartInline();
      Selection.StartParagraph.CallRequestTextLayoutInfoStart();
      SelectionChanged?.Invoke(Selection);

      if (!_isTypingEdit)
         _canCoalesceTyping = false;

   }

   internal void SelectionEnd_Changed(TextRange selRange, int newEnd)
   {
            
      selRange.EndParagraph = GetContainingParagraph(newEnd);
      
      selRange.EndParagraph.SelectionEndInBlock = newEnd - selRange.EndParagraph.StartInDoc;
    
      selRange.EndParagraph.CallRequestTextLayoutInfoEnd();
      selRange.GetEndInline();

      UpdateSelectedParagraphs();

      if (ShowDebugger)
         UpdateDebuggerSelectionParagraphs();

      //Make sure end is not less than start
      if (Selection.Length > 0)
         if (selRange.EndParagraph.SelectionEndInBlock < selRange.EndParagraph.SelectionStartInBlock)
            selRange.EndParagraph.SelectionStartInBlock = selRange.EndParagraph.SelectionEndInBlock;


      //Selection.EndParagraph.CallRequestInlinesUpdate();
      Selection.GetEndInline();
      Selection.EndParagraph.CallRequestTextLayoutInfoEnd();
      SelectionChanged?.Invoke(Selection);

      if (!_isTypingEdit)
         _canCoalesceTyping = false;

   }

   internal void UpdateSelectedParagraphs()
   {
      SelectionParagraphs.Clear();
      SelectionParagraphs.AddRange(Blocks.Where(p => p.StartInDoc + p.BlockLength > Selection.Start && p.StartInDoc <= Selection.End).ToList().ConvertAll(bb => (Paragraph)bb));
   }

   internal string GetText(TextRange tRange)
   {

      List<IEditable> rangeInlines = GetRangeInlines(tRange);
      return string.Join("", rangeInlines.ToList().ConvertAll(il => il.InlineText));

   }

   internal List<Block> GetRangeBlocks(TextRange trange)
   {
      //return Blocks.Where(b => b.IsParagraph && ((Paragraph)b).StartInDoc <= trange.End && b.StartInDoc + b.BlockLength - 1 >= trange.Start).ToList().ConvertAll(bb=>(Paragraph)bb);
      return Blocks.Where(b=> b.StartInDoc <= trange.End && b.StartInDoc + b.BlockLength - 1 >= trange.Start).ToList();
   }

   internal Paragraph GetContainingParagraph(int charIndex) => (Paragraph)Blocks.LastOrDefault(b => b.IsParagraph && ((Paragraph)b).StartInDoc <= charIndex)!;

   internal void UpdateBlockAndInlineStarts(int fromBlockIndex)
   {
      int parSum = fromBlockIndex == 0 ? 0 : Blocks[fromBlockIndex - 1].StartInDoc + Blocks[fromBlockIndex - 1].BlockLength;
      for (int parIndex = fromBlockIndex; parIndex < Blocks.Count; parIndex++)
      {
         Blocks[parIndex].StartInDoc = parSum;
         parSum += (Blocks[parIndex].BlockLength);

         if (Blocks[parIndex].IsParagraph)
            ((Paragraph)Blocks[parIndex]).UpdateEditableRunPositions();
      }
   }

   internal void UpdateBlockAndInlineStarts(Block thisBlock)
   {
      UpdateBlockAndInlineStarts(Blocks.IndexOf(thisBlock));
   }


   internal void ResetSelectionLengthZero(Paragraph currPar)
   {
      int startParIndex = Blocks.IndexOf(Selection!.StartParagraph);
      int endParIndex = Blocks.IndexOf(Selection!.EndParagraph);
      foreach (Paragraph p in Blocks.Where(pp => { int pindex = Blocks.IndexOf(pp); return pindex >= startParIndex && pindex <= endParIndex; }))
      {
         if (p != currPar)
            p.ClearSelection();
            
      }

   }

   internal void UpdateTextRanges(int editCharIndexStart, int offset)
   {
      List<TextRange> toRemoveRanges = [];
      
      int editCharIndexEnd = offset == 1 ? editCharIndexStart : editCharIndexStart - offset;

      foreach (TextRange trange in TextRanges)
      {
         if (trange.Equals(this.Selection)) continue;  //Don't update the selection range

         if (trange.Start >= editCharIndexStart && trange.End <= editCharIndexEnd)
            { toRemoveRanges.Add(trange); continue; }

         if (trange.Start >= editCharIndexStart)
         {
            if (trange.Start >= editCharIndexEnd)
               trange.Start += offset;
            else
               trange.Start = editCharIndexStart;
         }
            
         if (trange.End >= editCharIndexStart)
         {
            if (trange.End >= editCharIndexEnd)
               trange.End += offset;
            else
               trange.End = editCharIndexStart;
         }

         if (trange.Start > trange.End)
            trange.End = trange.Start;
      }

      for (int trangeNo = toRemoveRanges.Count - 1; trangeNo >=0; trangeNo--)
      {
         if (!toRemoveRanges[trangeNo].Equals(Selection))
            toRemoveRanges[trangeNo].Dispose();
      }
         

   }

   internal ExtendMode SelectionExtendMode { get; set; }

 
   internal enum ExtendMode
   {
      ExtendModeNone,
      ExtendModeRight,
      ExtendModeLeft
   }


}

