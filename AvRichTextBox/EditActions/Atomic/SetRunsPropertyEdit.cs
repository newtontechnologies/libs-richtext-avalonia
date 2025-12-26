using System.Collections.Generic;
using Avalonia;

namespace AvRichTextBox;

/// <summary>
/// Sets a single Avalonia property on multiple runs. Stores old values per run for reversibility.
/// </summary>
internal sealed class SetRunsPropertyEdit(
   AvaloniaProperty property,
   object? newValue,
   IReadOnlyDictionary<EditableRun, object?> oldValuesByRun) : IAtomicEdit
{
   public void Apply()
   {
      foreach (var kv in oldValuesByRun)
         kv.Key.SetValue(property, newValue);
   }

   public void Unapply()
   {
      foreach (var kv in oldValuesByRun)
         kv.Key.SetValue(property, kv.Value);
   }
}


