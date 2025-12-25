using Avalonia;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static AvRichTextBox.FlowDocument;

namespace AvRichTextBox;

public class RichTextBoxViewModel : INotifyPropertyChanged
{
   public event PropertyChangedEventHandler? PropertyChanged;
   private void NotifyPropertyChanged([CallerMemberName] String propertyName = "") { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

   public delegate void FlowDocChangedHandler();
   internal event FlowDocChangedHandler? FlowDocChanged;
      
   private Vector _rtbScrollOffset = new (0, 0);
   public Vector RTBScrollOffset { get => _rtbScrollOffset; set { if (_rtbScrollOffset != value) _rtbScrollOffset = value; NotifyPropertyChanged(nameof(RTBScrollOffset)); } }

   public double MinWidth => RunDebuggerVisible ? 500 : 100;

   private FlowDocument _flowDoc = null!;
   //public FlowDocument FlowDoc { get => _FlowDoc;  set { _FlowDoc = value; NotifyPropertyChanged(nameof(FlowDoc));  } }
   public FlowDocument FlowDoc { get => _flowDoc;  set { _flowDoc = value; NotifyPropertyChanged(nameof(FlowDoc)); FlowDocChanged?.Invoke();  } }

   private bool _runDebuggerVisible = false;
   public bool RunDebuggerVisible { get => _runDebuggerVisible; set { _runDebuggerVisible = value; NotifyPropertyChanged(nameof(RunDebuggerVisible)); } }

   public RichTextBoxViewModel()
   {
      //FlowDoc.ScrollInDirection += FlowDoc_ScrollInDirection;
      //FlowDoc.UpdateRTBCaret += FlowDoc_UpdateRTBCaret;
   }

   internal void FlowDoc_UpdateRTBCaret()
   {
      UpdateCaretVisible();
   }

   internal double ScrollViewerHeight = 10;
   
   private double _caretHeight = 5;
   public double CaretHeight { get => _caretHeight; set { _caretHeight = value; NotifyPropertyChanged(nameof(CaretHeight)); } }

   private Thickness _caretMargin = new (0);
   public Thickness CaretMargin { get => _caretMargin; set { _caretMargin = value; NotifyPropertyChanged(nameof(CaretMargin)); } }

   private bool _caretVisible = true;
   public bool CaretVisible { get => _caretVisible; set { _caretVisible = value; NotifyPropertyChanged(nameof(CaretVisible)); } }

   
   // FOR VISUAL CARET TESTING////////////////////////////////////////
   private double _lineHeightRectHeight = 5;
   public double LineHeightRectHeight { get => _lineHeightRectHeight; set { _lineHeightRectHeight = value; NotifyPropertyChanged(nameof(LineHeightRectHeight)); } }
   private Thickness _lineHeightRectMargin = new(0);
   public Thickness LineHeightRectMargin { get => _lineHeightRectMargin; set { _lineHeightRectMargin = value; NotifyPropertyChanged(nameof(LineHeightRectMargin)); } }
   private double _baseLineRectHeight = 5;
   public double BaseLineRectHeight { get => _baseLineRectHeight; set { _baseLineRectHeight = value; NotifyPropertyChanged(nameof(BaseLineRectHeight)); } }
   private Thickness _baseLineRectMargin = new(0);
   public Thickness BaseLineRectMargin { get => _baseLineRectMargin; set { _baseLineRectMargin = value; NotifyPropertyChanged(nameof(BaseLineRectMargin)); } }
   //////////////////////////////////////////////////////////////////


   internal void UpdateCaretVisible()
   {
      FlowDoc.Selection.StartParagraph?.CallRequestInvalidateVisual();
      CaretVisible = FlowDoc.Selection.Length == 0;
   }


   internal void FlowDoc_ScrollInDirection(int direction)
   {

      double scrollPadding = 30;
      if (direction == 1)
      {
         double checkPointY = FlowDoc.Selection!.EndRect!.Y;

         if (FlowDoc.SelectionExtendMode == ExtendMode.ExtendModeLeft)
            checkPointY = FlowDoc.Selection!.StartRect!.Y;

         if (checkPointY > RTBScrollOffset.Y + ScrollViewerHeight - scrollPadding)
            RTBScrollOffset = RTBScrollOffset.WithY(checkPointY - ScrollViewerHeight + scrollPadding);
            //RTBScrollOffset = RTBScrollOffset.WithY(checkPointY + scrollPadding);
      }
      else
      {
         double checkPointY = FlowDoc.Selection!.StartRect!.Y;
         if (FlowDoc.SelectionExtendMode == ExtendMode.ExtendModeRight)
            checkPointY = FlowDoc.Selection!.EndRect!.Y;

         if (checkPointY < RTBScrollOffset.Y)
            RTBScrollOffset = RTBScrollOffset.WithY(checkPointY);
      }
   }
}
