using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using static AvRichTextBox.HelperMethods;
using System.Xml;

namespace AvRichTextBox;

public partial class XamlConversions
{

   internal static string SectionTextDefault => "<Section xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">";
   internal static Uri PackageRelsUri = new(@"avares://AvRichTextBox/SaveAndLoadFormats/Xaml/XamlPackageData/.rels");
   internal static Uri ContentTypesUri = new(@"avares://AvRichTextBox/SaveAndLoadFormats/Xaml/XamlPackageData/[Content_Types].xml");

   //string ParagraphTextDefault => "<Paragraph LineHeight=\"18.666666666666668\" FontFamily=\"Times New Roman, ‚l‚r –¾’©\" Margin=\"0,0,0,0\" Padding=\"0,0,0,0\">";

   internal static void SaveXamlPackage(string fileName, FlowDocument fdoc)
   {

      using FileStream fstream = new(fileName, FileMode.Create);
      using ZipArchive zipArchive = new(fstream, ZipArchiveMode.Create);
      var resource = AssetLoader.Open(PackageRelsUri);
      var reader = new StreamReader(resource);
      ZipArchiveEntry relsEntry = zipArchive.CreateEntry("_rels/.rels");
      byte[] relsBytes = Encoding.UTF8.GetBytes(reader.ReadToEnd());
      using (var s = relsEntry.Open())
      { s.Write(relsBytes, 0, relsBytes.Length); }

      resource = AssetLoader.Open(ContentTypesUri);
      reader = new StreamReader(resource);
      ZipArchiveEntry contentsEntry = zipArchive.CreateEntry("[Content_Types].xml");
      byte[] contentsBytes = Encoding.UTF8.GetBytes(reader.ReadToEnd());
      using (var s = contentsEntry.Open())
      { s.Write(contentsBytes, 0, contentsBytes.Length); }


      //Save images, if any  
      List<Paragraph> imageContainingParagraphs = fdoc.Blocks.Where(b => b.IsParagraph && ((Paragraph)b).Inlines.Where(iline =>
          iline.GetType() == typeof(EditableInlineUiContainer) && ((EditableInlineUiContainer)iline).Child.GetType() == typeof(Image)).Any()).ToList().ConvertAll(bb => (Paragraph)bb);

      if (imageContainingParagraphs.Count != 0)
      {
         int imageNo = 0; //Consecutively define image nos.
         List<UniqueBitmap> uniqueBitmaps = [];
         foreach (Paragraph p in imageContainingParagraphs)
         {
            foreach (EditableInlineUiContainer imageUiContainer in p.Inlines.Where(iline => iline.GetType() == typeof(EditableInlineUiContainer) &&
                       ((EditableInlineUiContainer)iline).Child.GetType() == typeof(Image)))
            {
               Image? thisImg = imageUiContainer.Child as Image;

               Bitmap imgbitmap = (Bitmap)thisImg!.Source!;

               //Debug.WriteLine("Imagesource is null ? : " + (thisImg.Source == null));
               if (imgbitmap == null)
               {
                  //Create dummy bitmap to maintain doc/image structure
                  imageNo += 1;
                  Bitmap dummyBmp = new RenderTargetBitmap(new PixelSize(10, 10));
                  uniqueBitmaps.Add(new UniqueBitmap(dummyBmp, (int)thisImg.Width, (int)thisImg.Height, imageNo));
                  imageUiContainer.ImageNo = imageNo;
               }
               else
               {
                  UniqueBitmap foundUniqueBitmap = uniqueBitmaps.Where(bmp => bmp.UBitmap == imgbitmap).FirstOrDefault()!;
                  if (foundUniqueBitmap == null)
                  {  //add as new unique bitmap
                     imageNo += 1;
                     uniqueBitmaps.Add(new UniqueBitmap(imgbitmap, (int)thisImg.Width, (int)thisImg.Height, imageNo));
                     imageUiContainer.ImageNo = imageNo;
                  }
                  else
                  {
                     if (foundUniqueBitmap.MaxWidth < thisImg.Width)
                        foundUniqueBitmap.MaxWidth = (int)thisImg.Width;
                     if (foundUniqueBitmap.MaxHeight < thisImg.Height)
                        foundUniqueBitmap.MaxHeight = (int)thisImg.Height;
                     imageUiContainer.ImageNo = foundUniqueBitmap.ConsecutiveIndex;
                  }

               }
            }
         }

         //Write all unique bitmaps to zip
         imageNo = 1;
         foreach (UniqueBitmap uB in uniqueBitmaps)
         {
            //if (uB.uBitmap != null)
            //{
            var memoryStream = new MemoryStream();
            if (uB.UBitmap != null)
               ResizeAndSaveBitmap(uB.UBitmap, uB.MaxWidth, uB.MaxHeight, memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            string imageName = "Xaml/Image" + imageNo.ToString() + ".png";
            ZipArchiveEntry imageEntry = zipArchive.CreateEntry(imageName);
            using (var s = imageEntry.Open())
            {
               byte[] imgbytes = new byte[memoryStream.Length];
               memoryStream.Read(imgbytes, 0, imgbytes.Length);
               s.Write(imgbytes, 0, imgbytes.Length);
            }

            //}
            imageNo++;
         }
      }

      ZipArchiveEntry docEntry = zipArchive.CreateEntry("Xaml/Document.xaml");
      using (var s = docEntry.Open())
      {
         byte[] docBytes = Encoding.UTF8.GetBytes(GetDocXaml(true, fdoc));
         s.Write(docBytes, 0, docBytes.Length);
      }


      //Debug.WriteLine("done");


   }


   internal static string GetDocXaml(bool isXamlPackage, FlowDocument fdoc)
   {
      XmlDocument xamlDocument = new();
                  
      StringBuilder selXaml = new (SectionTextDefault);

      /////  make more general to use for any textrange 
      //selXaml.Append(GetBlocksXaml(new TextRange(this, 0, this.DocEndPoint)));
      
      foreach (Paragraph paragraph in fdoc.Blocks)
      {

         StringBuilder paragraphHeader = new ("<Paragraph ");
         paragraphHeader.Append(
            $"FontFamily=\"{paragraph.FontFamily.Name}" +
            $"\" FontWeight=\"{paragraph.FontWeight}" + 
            $"\" FontStyle=\"{paragraph.FontStyle}" + 
            $"\" FontSize=\"{paragraph.FontSize}" + 
            $"\" Margin=\"{paragraph.Margin}" + 
            $"\" Background=\"{paragraph.Background}" + 
            "\">");
         selXaml.Append(paragraphHeader);

         selXaml.Append(GetParagraphRunsXaml(paragraph.Inlines, isXamlPackage));
     
         selXaml.Append("</Paragraph>");
      }

      selXaml.Append("</Section>");

      return selXaml.ToString();

   }


   internal static string GetParagraphRunsXaml(IEnumerable<IEditable> parInlines, bool isXamlPackage)
   {
      StringBuilder runXamlBuilder = new();

      foreach (IEditable ied in parInlines)
      {
         switch (ied.GetType())
         {
            case Type t when t == typeof(EditableRun):

               EditableRun erun = (EditableRun)ied;
               StringBuilder runHeader = new("<Run ");
               runHeader.Append(GetRunAttributesString(erun));
               //Debug.WriteLine("runheader= " + GetRunAttributesString(erun));
               runXamlBuilder.Append(runHeader);
               runXamlBuilder.Append(erun.Text!.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"));
               runXamlBuilder.Append("</Run>");
               break;

            case Type t when t == typeof(EditableLineBreak):
               runXamlBuilder.Append("<LineBreak/>");
               break;

            case Type t when t == typeof(EditableInlineUiContainer):

               if (isXamlPackage)
               {
                  EditableInlineUiContainer eIuc = (EditableInlineUiContainer)ied;
                  string inlineUiHeader = $"<InlineUIContainer FontFamily=\"{eIuc.FontFamily.Name}\" BaselineAlignment=\"{eIuc.BaselineAlignment}\">";
                  runXamlBuilder.Append(inlineUiHeader);

                  if (eIuc.Child.GetType() == typeof(Image))
                  {
                     Image childImage = (Image)eIuc.Child;
                     string imageHeader = $"<Image Stretch=\"Fill\" Width=\"{childImage.Width}\" Height=\"{childImage.Height}\">";
                     runXamlBuilder.Append(imageHeader);

                     runXamlBuilder.Append(
                         "<Image.Source>" +
                         $"<BitmapImage UriSource=\"./Image{eIuc.ImageNo}.png\" CacheOption=\"OnLoad\" />" +
                         "</Image.Source>"
                     );

                     runXamlBuilder.Append("</Image>");
                  }

                  runXamlBuilder.Append("</InlineUIContainer>");
               }

               break;
         }
      }

      return runXamlBuilder.ToString();
   }


   internal static string GetRunAttributesString(EditableRun erun)
   {
      StringBuilder attSb = new ();

      attSb.Append($"FontFamily=\"{erun.FontFamily}\"");
      attSb.Append($" FontWeight=\"{erun.FontWeight}\"");
      attSb.Append($" FontSize=\"{erun.FontSize}\"");
      attSb.Append($" FontStyle=\"{erun.FontStyle}\"");
      attSb.Append($" FontStretch=\"{erun.FontStretch}\"");
      attSb.Append($" BaselineAlignment=\"{erun.BaselineAlignment}\"");

      if (erun.Foreground != null)
         attSb.Append($" Foreground=\"{erun.Foreground}\"");
      if (erun.Background != null)
         attSb.Append($" Background=\"{erun.Background}\"");

      if (erun.TextDecorations != null)
      {
         string textDecString = "";
         if (erun.TextDecorations.Count > 0)
         {
            textDecString = " TextDecorations=\"";
            switch (erun.TextDecorations[0].Location)
            {
               case TextDecorationLocation.Underline:
                  textDecString += "Underline";
                  break;
               case TextDecorationLocation.Baseline:
                  textDecString += "Baseline";
                  break;
               case TextDecorationLocation.Overline:
                  textDecString += "Overline";
                  break;
            }
            textDecString += "\"";
         }
         attSb.Append(textDecString);
      }
      
      //Closing bracket
      attSb.Append('>');

      return attSb.ToString();
   }



}


