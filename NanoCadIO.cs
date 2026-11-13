using System;
using System.Windows;
using Teigha.Runtime;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;
using HostMgd.Windows;
using HostMgd.EditorInput;


namespace NanoCadIO
{
	public class LayoutCommands {
		[CommandMethod("IO_SWITCH_TO_NEXT_LAYOUT")]
		public static void SwitchToNextLayout() {
			Document doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;
			List<string> layout_names = GetLayoutNamesInTabOrder();
			LayoutManager layoutManager = LayoutManager.Current;
			string current_layout = layoutManager.CurrentLayout;
			int current_layout_index = layout_names.FindIndex(x => x == current_layout);
			int switch_to_index = (current_layout_index + 1 + layout_names.Count) % layout_names.Count;
			ed.WriteMessage($"Switching to {switch_to_index}");
			string next_layout = layout_names[switch_to_index];
			layoutManager.CurrentLayout = next_layout;
		}
		[CommandMethod("IO_SWITCH_TO_PREVIOUS_LAYOUT")]
		public static void SwitchToPreviousLayout() {
			Document doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;
			List<string> layout_names = GetLayoutNamesInTabOrder();
			LayoutManager layoutManager = LayoutManager.Current;
			string current_layout = layoutManager.CurrentLayout;
			int current_layout_index = layout_names.FindIndex(x => x == current_layout);
			int switch_to_index = (current_layout_index - 1 + layout_names.Count) % layout_names.Count;
			ed.WriteMessage($"Switching to {switch_to_index}");
			string previous_layout = layout_names[switch_to_index];
			layoutManager.CurrentLayout = previous_layout;
		}
		public static void ListAllLayouts() {
			Document doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;

			DBDictionary layoutDictionart = GetLayoutDictionary();

			// foreach
			ed.WriteMessage($"\nЛисты: {String.Join(", ", GetLayoutNamesInTabOrder())}");
		}
		public static List<string> GetLayoutNames() {
			List<string> layoutNames = new List<string>();
			Database database = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database;
			DBDictionary layoutDictionary = GetLayoutDictionary();

			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				foreach (DBDictionaryEntry entry in layoutDictionary)
				{
					Layout layout = (Layout)transaction.GetObject(
						entry.Value,
						OpenMode.ForRead
					);
					layoutNames.Add(layout.LayoutName);
				}
			}

			return layoutNames;
		}
		public static DBDictionary GetLayoutDictionary() {
			List<string> layoutNames = new List<string>();
			DBDictionary layoutDictionary;
			Database database = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database;

			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				layoutDictionary = (DBDictionary)transaction.GetObject(
					database.LayoutDictionaryId,
					OpenMode.ForRead
				);
			}
			return layoutDictionary;
		}
		public static List<string> GetLayoutNamesInTabOrder() {
			var layoutNames = new List<string>();
			Database db = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Database;
			
			using (Transaction tr = db.TransactionManager.StartTransaction())
			{
				var layoutDict = (DBDictionary)tr.GetObject(
					db.LayoutDictionaryId,
					OpenMode.ForRead
				);
				var layoutsByTab = new SortedDictionary<int, string>();

				foreach (DBDictionaryEntry entry in layoutDict)
				{
					var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
					layoutsByTab[layout.TabOrder] = layout.LayoutName;
				}

				layoutNames.AddRange(layoutsByTab.Values);
			}

			return layoutNames;
		}
	}
	public class BlockInfo {
		[CommandMethod("IO_GET_BLOCKS")]
		public static void GetBlocks() {
			Document document = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			Editor editor = document.Editor;
			Database database = document.Database;
			using (Transaction transaction = document.TransactionManager.StartTransaction()) {
				var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
				foreach (ObjectId btrObj in blockTable) {
					if (transaction.GetObject(btrObj, OpenMode.ForRead) is not BlockTableRecord blockTableRecord)
					{
						continue;
					}
					if (blockTableRecord.IsLayout | blockTableRecord.Name.StartsWith("_"))
					{
						continue;
					}
					editor.WriteMessage($"Имя блока: {blockTableRecord.Name}\tДинамический: {blockTableRecord.IsDynamicBlock}");
					ObjectIdCollection blockReferenceIds = blockTableRecord.GetBlockReferenceIds(true, true);
					foreach (ObjectId brObj in blockReferenceIds)
					{
						if (transaction.GetObject(brObj, OpenMode.ForRead) is not BlockReference blockReference)
						{
							continue;
						}

						editor.WriteMessage($"\n\tЭкземпляр блока {blockReference.Name}:");

						if (blockReference.IsDynamicBlock)
						{
							editor.WriteMessage("\n\t\t--- Динамические свойства ---");
							DynamicBlockReferencePropertyCollection dynamicProperties = blockReference.DynamicBlockReferencePropertyCollection;
							string value;
							foreach (DynamicBlockReferenceProperty dynamicProperty in dynamicProperties)
							{
								if (dynamicProperty == null) continue;
								if (dynamicProperty.Show == false) continue;
								if (dynamicProperty.Value is object[] arrayValue)
									value = string.Join(", ", arrayValue);
								else
									value = dynamicProperty.Value?.ToString() ?? "<null>";
								editor.WriteMessage($"\n\t\t{dynamicProperty.PropertyName}: {value}");
							}
						}
						
						if (blockReference.AttributeCollection.Count > 0)
							editor.WriteMessage("\n\t\t--- Атрибуты ---");
						foreach (ObjectId attrObj in blockReference.AttributeCollection)
						{
							if (transaction.GetObject(attrObj, OpenMode.ForRead) is not AttributeReference attributeReference)
								continue;
							if (attributeReference.Invisible)
								continue;
							editor.WriteMessage($"\n\t\t{attributeReference.Tag}: {attributeReference.TextString}");
						}
					}
				}
			}
		}
	}
	public class PaletteCommands {
		private static PaletteSet? _paletteSet;
		[CommandMethod("IO_SHOW_BLOCKS_IO_PALETTE")]
		public static void ShowBlocksIOPalette()
		{
			if (_paletteSet == null)
			{
				_paletteSet = new PaletteSet("Блоки в Excel");
				var ioControl = new BlocksIoPalette();
				
				// Добавляем наш UserControl на панель
				_paletteSet.AddVisual("Экспорт/Импорт", ioControl);
				
				_paletteSet.MinimumSize = new System.Drawing.Size(350, 500);
				_paletteSet.Dock = DockSides.None;
			}
			_paletteSet.Visible = true;
		}
	}
}
