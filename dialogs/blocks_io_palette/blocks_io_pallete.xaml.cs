// dialogs/blocks_io_palette/blocks_io_pallete.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;
using HostMgd.EditorInput;
using NanoCadIO.BlockIO;
using AcApp = HostMgd.ApplicationServices.Application;

namespace NanoCadIO
{
	public partial class BlocksIoPalette : System.Windows.Controls.UserControl
	{
		public ObservableCollection<BlockSelectionItem> BlockItems { get; set; } = new();

		private Document? _doc;

		public BlocksIoPalette()
		{
			InitializeComponent();
			BlocksList.ItemsSource = BlockItems;

			Loaded += BlocksIoPalette_Loaded;
			Unloaded += BlocksIoPalette_Unloaded;
		}

		private void BlocksIoPalette_Loaded(object sender, RoutedEventArgs e)
		{
			// на случай повторной загрузки — не подписываемся дважды
			AcApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
			AcApp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;

			AttachToDocument(AcApp.DocumentManager.MdiActiveDocument);
			RefreshBlockList();
			SyncSelectionFromDrawing();
		}

		private void BlocksIoPalette_Unloaded(object sender, RoutedEventArgs e)
		{
			AcApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
			DetachFromDocument();
		}

		// Исправлено: object? sender (для соответствия EventHandler в .NET 6+)
		private void DocumentManager_DocumentActivated(object? sender, DocumentCollectionEventArgs e)
		{
			AttachToDocument(e.Document);
			RefreshBlockList();
			SyncSelectionFromDrawing();
		}

		private void AttachToDocument(Document? doc)
		{
			if (ReferenceEquals(_doc, doc))
				return;

			DetachFromDocument();
			_doc = doc;
			if (_doc != null)
				_doc.ImpliedSelectionChanged += Doc_ImpliedSelectionChanged;
		}

		private void DetachFromDocument()
		{
			if (_doc == null)
				return;

			_doc.ImpliedSelectionChanged -= Doc_ImpliedSelectionChanged;
			_doc = null;
		}

		// Исправлено: object? sender (это исправляет ошибку CS8622)
		private void Doc_ImpliedSelectionChanged(object? sender, EventArgs e)
		{
			SyncSelectionFromDrawing();
		}

		/// <summary>
		/// Отмечает в списке блоки, которые сейчас выделены в чертеже (pickfirst).
		/// </summary>
		private void SyncSelectionFromDrawing()
		{
			var doc = AcApp.DocumentManager.MdiActiveDocument;
			if (doc == null || BlockItems.Count == 0)
				return;

			var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				var pickfirst = doc.Editor.SelectImplied();
				if (pickfirst.Status == PromptStatus.OK && pickfirst.Value != null)
				{
					using var tr = doc.TransactionManager.StartTransaction();
					foreach (SelectedObject selObj in pickfirst.Value)
					{
						if (selObj == null)
							continue;
						if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is not BlockReference blockRef)
							continue;

						string name = blockRef.Name;
						if (blockRef.IsDynamicBlock)
						{
							var dynBtr = (BlockTableRecord)tr.GetObject(blockRef.DynamicBlockTableRecord, OpenMode.ForRead);
							name = dynBtr.Name;
						}
						selectedNames.Add(name);
					}
				}
			}
			catch (Exception ex)
			{
				Log(doc, $"Не удалось прочитать текущее выделение: {ex.Message}");
				return;
			}

			foreach (var item in BlockItems)
				item.IsSelected = selectedNames.Contains(item.BlockName);
		}

		private void RefreshBlockList()
		{
			BlockItems.Clear();
			var doc = AcApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			try
			{
				using var tr = doc.TransactionManager.StartTransaction();
				var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);

				foreach (ObjectId btrId in blockTable)
				{
					var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

					// Пропускаем листы (Layouts) и системные/анонимные блоки (начинаются с * или _)
					if (btr.IsLayout || btr.Name.StartsWith("*") || btr.Name.StartsWith("_"))
						continue;

					BlockItems.Add(new BlockSelectionItem { BlockName = btr.Name, IsSelected = false });
				}
				Log(doc, $"Загружено {BlockItems.Count} определений блоков.");
			}
			catch (Exception ex)
			{
				Log(doc, $"Ошибка загрузки списка: {ex.Message}");
			}
		}

		private void RefreshBtn_Click(object sender, RoutedEventArgs e)
		{
			RefreshBlockList();
			SyncSelectionFromDrawing();
		}

		private void ExportBtn_Click(object sender, RoutedEventArgs e)
		{
			var doc = AcApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			var selectedBlocks = BlockItems.Where(b => b.IsSelected).Select(b => b.BlockName).ToHashSet();
			if (selectedBlocks.Count == 0)
			{
				Log(doc, "Не выбрано ни одного блока для экспорта.");
				return;
			}

			var saveDialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Excel Files (*.xlsx)|*.xlsx",
				DefaultExt = "xlsx",
				FileName = "blocks_export.xlsx"
			};

			if (saveDialog.ShowDialog() != true) return;

			try
			{
				var blocks = BlockDataExtractor.Extract(doc, selectedBlocks);
				if (blocks.Count == 0)
				{
					Log(doc, "Не найдено ни одного вхождения выбранных блоков в текущем чертеже.");
					return;
				}

				ExcelExporter.Export(blocks, saveDialog.FileName);
				Log(doc, $"Успешно экспортировано {blocks.Count} вхождений в {saveDialog.FileName}");
			}
			catch (Exception ex)
			{
				Log(doc, $"Ошибка экспорта: {ex.Message}");
			}
		}

		private void ImportBtn_Click(object sender, RoutedEventArgs e)
		{
			var doc = AcApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			var openDialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Excel Files (*.xlsx)|*.xlsx",
				DefaultExt = "xlsx"
			};

			if (openDialog.ShowDialog() != true) return;

			try
			{
				int updated = ExcelImporter.Import(doc, openDialog.FileName);
				Log(doc, $"Импорт завершён. Обновлено блоков: {updated}");
				SyncSelectionFromDrawing();
			}
			catch (Exception ex)
			{
				Log(doc, $"Ошибка импорта: {ex.Message}");
			}
		}

		private static void Log(Document? doc, string message)
		{
			doc?.Editor?.WriteMessage($"\n[Blocks IO] {message}");
		}
	}
}
