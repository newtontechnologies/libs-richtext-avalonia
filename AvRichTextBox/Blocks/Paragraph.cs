using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AvRichTextBox;

public class Paragraph : Block
{

   public ObservableCollection<IEditable> Inlines { get; set; } = [];

   public Paragraph()
   {
      Inlines.CollectionChanged += Inlines_CollectionChanged;
   }

   private void Inlines_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
   {
      foreach (IEditable ied in Inlines)
         ied.MyParagraph = this;
            
   }

   public string ParToolTip => $"Background: {Background}\nLineSpacing: {LineSpacing}\nLineHeight: {LineHeight}";
   //public string ParToolTip => $"Background: {Background}\nLineHeight: {LineHeight}";
   
   //public string Text => string.Join("", Inlines.ToList().ConvertAll(ied => ied.InlineText));

   private Thickness _borderThickness = new Thickness(0);
   public Thickness BorderThickness { get => _borderThickness; set { _borderThickness = value; NotifyPropertyChanged(nameof(BorderThickness)); } }

   private SolidColorBrush _borderBrush = new (Colors.Transparent);
   public SolidColorBrush BorderBrush { get => _borderBrush; set { _borderBrush = value; NotifyPropertyChanged(nameof(BorderBrush)); } }

   private SolidColorBrush _background = new (Colors.Transparent);
   public SolidColorBrush Background { get => _background; set { _background = value; NotifyPropertyChanged(nameof(Background)); } }

   //private FontFamily _FontFamily = new ("ＭＳ 明朝, Times New Roman");
   private FontFamily _fontFamily = new ("Meiryo");
   //private FontFamily _FontFamily = "Meiryo";
   public FontFamily FontFamily { get => _fontFamily; set { _fontFamily = value; NotifyPropertyChanged(nameof(FontFamily)); } }

   private double _fontSize = 16D;
   public double FontSize { get => _fontSize; set { _fontSize = value; NotifyPropertyChanged(nameof(FontSize)); } }

   private double _lineHeight = 18.666D;  // fontsize normally
   public double LineHeight { get => _lineHeight; set { _lineHeight = value; NotifyPropertyChanged(nameof(LineHeight)); CallRequestInlinesUpdate(); CallRequestTextLayoutInfoStart(); } }

   private double _lineSpacing = 0D;
   public double LineSpacing { get => _lineSpacing; set { _lineSpacing = value; NotifyPropertyChanged(nameof(LineSpacing)); CallRequestInlinesUpdate(); CallRequestTextLayoutInfoStart(); } }

   private FontWeight _fontWeight = FontWeight.Normal;
   public FontWeight FontWeight { get => _fontWeight; set { _fontWeight = value; NotifyPropertyChanged(nameof(FontWeight)); } }

   private FontStyle _fontStyle = FontStyle.Normal;
   public FontStyle FontStyle{ get => _fontStyle; set { _fontStyle = value; NotifyPropertyChanged(nameof(FontStyle)); } }

   private TextAlignment _textAlignment = TextAlignment.Left;
   public TextAlignment TextAlignment { get => _textAlignment; set { _textAlignment = value; NotifyPropertyChanged(nameof(TextAlignment)); } }

   //private SolidColorBrush _SelectionForegroundBrush = new (Colors.Black);  // in Avalonia > 11.1, setting this alters the selection font for some reason
   //public SolidColorBrush SelectionForegroundBrush { get => _SelectionForegroundBrush; set { _SelectionForegroundBrush = value; NotifyPropertyChanged(nameof(SelectionForegroundBrush)); } }

   private SolidColorBrush _selectionBrush = LightBlueBrush;
   public SolidColorBrush SelectionBrush { get => _selectionBrush; set { _selectionBrush = value; NotifyPropertyChanged(nameof(SelectionBrush)); } }
   internal static SolidColorBrush LightBlueBrush = new(Colors.LightBlue);

   internal double DistanceSelectionEndFromLeft = 0;
   internal double DistanceSelectionStartFromLeft = 0;
   internal int CharNextLineEnd = 0;
   internal int CharPrevLineEnd = 0;
   internal int CharNextLineStart = 0;
   internal int CharPrevLineStart = 0;
   internal int FirstIndexStartLine = 0;  //For home key
   internal int LastIndexEndLine = 0;  //For end key
   internal int FirstIndexLastLine = 0;  //For moving to previous paragraph

   internal bool IsStartAtFirstLine = false;
   internal bool IsEndAtFirstLine = false;
   internal bool IsStartAtLastLine = false;
   internal bool IsEndAtLastLine = false;

   private bool _requestInlinesUpdate;
   internal bool RequestInlinesUpdate { get => _requestInlinesUpdate; set { _requestInlinesUpdate = value; NotifyPropertyChanged(nameof(RequestInlinesUpdate)); } }

   private bool _requestInvalidateVisual;
   internal bool RequestInvalidateVisual { get => _requestInvalidateVisual; set { _requestInvalidateVisual = value; NotifyPropertyChanged(nameof(RequestInvalidateVisual)); } }

   private bool _requestTextLayoutInfoStart;
   internal bool RequestTextLayoutInfoStart { get => _requestTextLayoutInfoStart; set { _requestTextLayoutInfoStart = value; NotifyPropertyChanged(nameof(RequestTextLayoutInfoStart)); } }

   private bool _requestTextLayoutInfoEnd;
   internal bool RequestTextLayoutInfoEnd { get => _requestTextLayoutInfoEnd; set { _requestTextLayoutInfoEnd = value; NotifyPropertyChanged(nameof(RequestTextLayoutInfoEnd)); } }

   private bool _requestTextBoxFocus;
   public bool RequestTextBoxFocus { get => _requestTextBoxFocus; set { _requestTextBoxFocus = value; NotifyPropertyChanged(nameof(RequestTextBoxFocus)); } }

   //private int _RequestRectOfCharacterIndex;
   //public int RequestRectOfCharacterIndex { get => _RequestRectOfCharacterIndex; set { _RequestRectOfCharacterIndex = value; NotifyPropertyChanged(nameof(RequestRectOfCharacterIndex)); } }

   internal void CallRequestTextBoxFocus() { RequestTextBoxFocus = true; RequestTextBoxFocus = false; }
   internal void CallRequestInvalidateVisual() { RequestInvalidateVisual = true; RequestInvalidateVisual = false; }
   internal void CallRequestInlinesUpdate() { RequestInlinesUpdate = true; RequestInlinesUpdate = false; }
   internal void CallRequestTextLayoutInfoStart() { RequestTextLayoutInfoStart = true; RequestTextLayoutInfoStart = false; }
   internal void CallRequestTextLayoutInfoEnd() { RequestTextLayoutInfoEnd = true; RequestTextLayoutInfoEnd = false; }
   //internal void CallRequestTextLayoutInfoStart() { RequestTextLayoutInfoStart = false; RequestTextLayoutInfoStart = true; }
   //internal void CallRequestTextLayoutInfoEnd() { RequestTextLayoutInfoEnd = false; RequestTextLayoutInfoEnd = true; }

   internal void UpdateEditableRunPositions()
   {
      int sum = 0;
      for (int edx = 0; edx < Inlines.Count; edx++)
      {
         Inlines[edx].TextPositionOfInlineInParagraph = sum;
         sum += Inlines[edx].InlineLength;
      }
   }

   internal void UpdateUiContainersSelected()
   {
      if (this.Inlines != null)
      {

         IEditable? startInline = Inlines.FirstOrDefault(il => il.IsStartInline);
         IEditable? endInline = Inlines.FirstOrDefault(il => il.IsEndInline);
         foreach (EditableInlineUiContainer iuc  in this.Inlines.OfType<EditableInlineUiContainer>())
         {
            int stidx = startInline == null ? -1 : this.Inlines.IndexOf(startInline);
            int edidx = endInline == null ? Int32.MaxValue : this.Inlines.IndexOf(endInline);
            int thisidx = this.Inlines.IndexOf(iuc);
            iuc.IsSelected = (thisidx > stidx && thisidx < edidx);
         }
      }

   }

   internal Paragraph PropertyClone()
   {
      return new Paragraph() 
      { 
         TextAlignment = this.TextAlignment,
         LineSpacing = this.LineSpacing,
         BorderBrush = this.BorderBrush,
         BorderThickness = this.BorderThickness,
         LineHeight = this.LineHeight,
         Margin= this.Margin,
         Background = this.Background,
         FontFamily = this.FontFamily,
         FontSize = this.FontSize,
         FontStyle = this.FontStyle,
         FontWeight = this.FontWeight
      }; 
   }

   internal Paragraph FullClone()
   {
      return new Paragraph() 
      { 
         TextAlignment = this.TextAlignment,
         LineSpacing = this.LineSpacing,
         BorderBrush = this.BorderBrush,
         BorderThickness = this.BorderThickness,
         LineHeight = this.LineHeight,
         Margin= this.Margin,
         Background = this.Background,
         FontFamily = this.FontFamily,
         FontSize = this.FontSize,
         FontStyle = this.FontStyle,
         FontWeight = this.FontWeight,
         Inlines = new ObservableCollection<IEditable>(this.Inlines.Select(il=>il.Clone()))
      }; 
   }
}