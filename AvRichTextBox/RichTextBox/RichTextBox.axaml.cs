using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace AvRichTextBox;

public partial class RichTextBox : UserControl
{
   internal FlowDocument FlowDoc => RtbVm.FlowDoc;
   private RichTextBoxViewModel RtbVm { get; set; } = new();

   private void ToggleDebuggerPanel (bool visible) { RunDebugPanel.IsVisible = visible; }

   public void ScrollToSelection()
   {
      RtbVm.RTBScrollOffset = RtbVm.RTBScrollOffset.WithY(FlowDoc.Selection.StartRect.Y - 50);
   }

   public RichTextBox()
   {
      InitializeComponent();

      this.PropertyChanged += RichTextBox_PropertyChanged;

      FlowDocument = new FlowDocument();

      RtbVm.FlowDocChanged += RtbVM_FlowDocChanged;

      MainDP.DataContext = RtbVm;  // bind to child DockPanel, not the UserControl itself

      this.Loaded += RichTextBox_Loaded;
      
      FlowDocSV.SizeChanged += FlowDocSV_SizeChanged;

      AdornerLayer.SetAdorner(DocIC, _caretRect);

      InitializeBlinkAnimation();

      _blinkAnimation.RunAsync(_caretRect);
      _caretRect.Bind(IsVisibleProperty, new Binding("CaretVisible"));
      _caretRect.Bind(MarginProperty, new Binding("CaretMargin"));
      _caretRect.Bind(HeightProperty, new Binding("CaretHeight"));
      _caretRect.DataContext = RtbVm;

      this.TextInput += RichTextBox_TextInput;

      this.Focusable = true;

      this.GotFocus += RichTextBox_GotFocus;
      this.LostFocus += RichTextBox_LostFocus;
   }

   private void RichTextBox_Loaded(object? sender, RoutedEventArgs e)
   {
      if (ShowDebuggerPanelInDebugMode)
      {
#if DEBUG
         RtbVm.RunDebuggerVisible = ShowDebuggerPanelInDebugMode;
         //RunDebugger.DataContext = FlowDoc;  // binding set in Xaml
         this.Width = this.Width + (RtbVm.RunDebuggerVisible ? 400 : 0);
#else
      RunDebugger.DataContext = null;
#endif
      }

      FlowDoc.ShowDebugger = RtbVm.RunDebuggerVisible;

      this.Focus();

      //FlowDoc.NewDocument();

      //CreateClient();
   }

   private void RtbVM_FlowDocChanged()
   {
      DocIC.DataContext = RtbVm.FlowDoc;
      UpdateAllInlines();
   }

   private void RichTextBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
   {
      
      if (e.Property.Name == "FlowDocument")
      {
         if (FlowDoc != null)
         {
            FlowDoc.ScrollInDirection -= RtbVm.FlowDoc_ScrollInDirection;
            FlowDoc.UpdateRtbCaret -= RtbVm.FlowDoc_UpdateRTBCaret;
         }

         RtbVm.FlowDoc = FlowDocument;

         RtbVm.FlowDoc.ScrollInDirection += RtbVm.FlowDoc_ScrollInDirection;
         RtbVm.FlowDoc.UpdateRtbCaret += RtbVm.FlowDoc_UpdateRTBCaret;
         RtbVm.FlowDoc.InitializeDocument();
         CreateClient();

      }

   }

   private void RichTextBox_GotFocus(object? sender, GotFocusEventArgs e)
   {
      //Debug.WriteLine("Got focus rtb");
   }

   private void RichTextBox_LostFocus(object? sender, RoutedEventArgs e)
   {
      //Debug.WriteLine("lost focus rtb");
   }


   internal void UpdateAllInlines()
   {
      foreach (Paragraph p in FlowDoc.Blocks.Where(b => b.IsParagraph))
      {
         p.CallRequestInlinesUpdate();
         p.CallRequestInvalidateVisual();

      }
   }


   internal void CreateClient()
   {
      InputMethod.SetIsInputMethodEnabled(this, true);
      this.TextInputMethodClientRequested += RichTextBox_TextInputMethodClientRequested;

      _client ??= new RichTextBoxTextInputClient(this);
      //Debug.WriteLine("created new client)");

      this.Focus();

   }

   private RichTextBoxTextInputClient? _client;

   private void RichTextBox_TextInputMethodClientRequested(object? sender, TextInputMethodClientRequestedEventArgs e)
   {
      _client ??= new RichTextBoxTextInputClient(this);
      e.Client = _client;
      //Debug.WriteLine("e.Client requested = " + e.Client.Selection.ToString());

   }

   string _preeditText = "";

   internal void InsertPreeditText(string preeditText)
   {      
      _preeditText = preeditText;
      //Debug.WriteLine("preditexttext = *" + _preeditText + "*");
      UpdatePreeditOverlay();
   }

   internal Point CaretPosition { get; set; }

   private void UpdatePreeditOverlay()
   {
      if (!string.IsNullOrEmpty(_preeditText))
      {
         double cX = _caretRect.Margin.Left - 2;
         double cY = _caretRect.Margin.Top - 2;

         PreeditOverlay.Text = _preeditText;
         PreeditOverlay.Margin = new Thickness(cX, cY, 0, 0);
         PreeditOverlay.IsVisible = true;
         CaretPosition = new Point(cX, cY - RtbVm.RTBScrollOffset.Y);
         _client?.UpdateCaretPosition();

      }
      else
      {
         PreeditOverlay.IsVisible = false;
      }
   }
   
   private readonly Rectangle _caretRect = new()
   {
      StrokeThickness = 2,
      Stroke = Brushes.Black,
      Height = 20,
      Width = 1.5,
      IsVisible = false,
      HorizontalAlignment = HorizontalAlignment.Left,
      VerticalAlignment = VerticalAlignment.Top,
      IsHitTestVisible = false
   };


   public void NewDocument() { FlowDoc.NewDocument(); }
   public void CloseDocument() { FlowDoc.NewDocument();  RtbVm.RTBScrollOffset = new Vector(0, 0);  }
   //Load/save
	public void LoadRtf(string rtf) { FlowDoc.LoadRtf(rtf); }
   public void LoadRtfDoc(string fileName) { FlowDoc.LoadRtfFromFile(fileName);  }

	public string SaveRtf() { return FlowDoc.SaveRtf(); }
   public void SaveRtfDoc(string fileName) { FlowDoc.SaveRtfToFile(fileName);  }
   public void LoadWordDoc(string fileName) { FlowDoc.LoadWordDocFromFile(fileName);  }
   public void SaveWordDoc(string filename) { FlowDoc.SaveWordDocToFile(filename); }
	public void LoadHtml(string html) { FlowDoc.LoadHtml(html); }

	public string SaveHtml() { return FlowDoc.SaveHtml(); }
   public void LoadHtmlDoc(string fileName) { FlowDoc.LoadHtmlDocFromFile(fileName);  }
   public void SaveHtmlDoc(string filename) { FlowDoc.SaveHtmlDocToFile(filename); }
	
   public void LoadXaml (string fileName) { FlowDoc.LoadXamlFromFile(fileName); }
   public void SaveXamlPackage (string fileName) { FlowDoc.SaveXamlPackage(fileName); }
	public void LoadXamlString(string xaml) { FlowDoc.LoadXaml(xaml); }
	public string SaveXamlString() { return FlowDoc.SaveXaml(); }
   public void SaveXaml (string fileName) { FlowDoc.SaveXamlToFile(fileName); }
   public void LoadXamlPackage (string fileName) { FlowDoc.LoadXamlPackage(fileName);  }

   private void MovePage(int direction, bool extend)
   {  //Debug.WriteLine("trying to move page");

      double currentY = 0;
      switch (FlowDoc.SelectionExtendMode)
      {
         case FlowDocument.ExtendMode.ExtendModeRight:
         case FlowDocument.ExtendMode.ExtendModeNone:
            currentY = FlowDoc.Selection!.EndRect!.Y;
            break;

         case FlowDocument.ExtendMode.ExtendModeLeft:
            currentY = FlowDoc.Selection!.StartRect!.Y;
            break;
      }

      double distanceFromTop = currentY - RtbVm.RTBScrollOffset.Y;
      double distanceFromLeft = FlowDoc.Selection!.StartRect!.X + FlowDocSV.Margin.Left;
      double newScrollY = RtbVm.RTBScrollOffset.Y + FlowDocSV.Bounds.Height * direction;
      RtbVm.RTBScrollOffset = RtbVm.RTBScrollOffset.WithY(newScrollY);
      double newCaretY = newScrollY + distanceFromTop;
      //Debug.WriteLine("\nnewCaretY = " + newCaretY + "\nnewscrollY= " + newScrollY + "\ndistanceTop=" + distanceFromTop);
      //EditableParagraph? thisEP = DocIC.GetVisualDescendants().OfType<EditableParagraph>().Where(ep => ep.TranslatePoint(ep.Bounds.Position, DocIC)!.Value.Y <= newCaretY).LastOrDefault();
      EditableParagraph? thisEp = DocIC.GetVisualDescendants().OfType<EditableParagraph>().Where(ep => ep.TranslatePoint(ep.Bounds.Position, DocIC)!.Value.Y <= newScrollY).LastOrDefault();
      

      if (thisEp == null)
      {
         if (direction == -1)
         {
            if (FlowDoc.SelectionExtendMode == FlowDocument.ExtendMode.ExtendModeRight)
            {
               FlowDoc.Select(0, 0);
               FlowDoc.SelectionExtendMode = FlowDocument.ExtendMode.ExtendModeNone;
            }
            else
            {
               FlowDoc.MovePageSelection(-1, extend, 0);
            }

            this.Focus();
         }
      }
      else
      {
         double relYInEp = newCaretY - thisEp!.TranslatePoint(thisEp!.Bounds.Position, DocIC)!.Value.Y + 18;
         TextHitTestResult tres = thisEp.TextLayout.HitTestPoint(new Point(distanceFromLeft, relYInEp));
         int newCharIndexInDoc = ((Paragraph)thisEp.DataContext!).StartInDoc + tres.CharacterHit.FirstCharacterIndex;
         FlowDoc.MovePageSelection(direction, extend, newCharIndexInDoc + (int)(FlowDocSV.Bounds.Height / 2));
      }

   }


   private void FlowDocSV_SizeChanged(object? sender, SizeChangedEventArgs e)
   {
      RtbVm.ScrollViewerHeight = e.NewSize.Height;

   }
     
   private Animation _blinkAnimation;

   private void InitializeBlinkAnimation()
   {
      _blinkAnimation = new Animation()
      {
         Duration = TimeSpan.FromSeconds(0.85),
         FillMode = FillMode.Forward,
         IterationCount = IterationCount.Infinite,
         Children =
            {
                new KeyFrame { Cue = new (0.0), Setters = { new Setter(Rectangle.OpacityProperty, 0.0) } },
                new KeyFrame { Cue = new (0.5), Setters = { new Setter(Rectangle.OpacityProperty, 1.0) } },
                new KeyFrame { Cue = new (1.0), Setters = { new Setter(Rectangle.OpacityProperty, 0.0) } }
            }
      };
   }


   private void ScrollViewer_ScrollChanged(object? sender, Avalonia.Controls.ScrollChangedEventArgs e)
   {
      RtbVm.RTBScrollOffset = FlowDocSV.Offset;

   }


}

