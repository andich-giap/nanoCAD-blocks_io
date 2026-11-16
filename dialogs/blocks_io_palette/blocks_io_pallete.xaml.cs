using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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

		private readonly ICollectionView _blockItemsView;
		private Document? _doc;

		// Кэш последнего состояния выделения — чтобы не перечитывать чертёж
		// при каждом клике по чекбоксу.
		private int _lastSelectedBlockCount;
		private int _lastSelectedTypeCount;

		public BlocksIoPalette()
		{
			InitializeComponent();

			_blockItemsView = CollectionViewSource.GetDefaultView(BlockItems);
			_blockItemsView.SortDescriptions.Add(
				new SortDescription(nameof(BlockSelectionItem.BlockName), ListSortDirection.Ascending));
			BlocksList.ItemsSource = _blockItemsView;

			Loaded += BlocksIoPalette_Loaded;
			Unloaded += BlocksIoPalette_Unloaded;
		}

		private void BlocksIoPalette_Loaded(object sender, RoutedEventArgs e)
		{
			AcApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
			AcApp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;

			AttachToDocument(AcApp.DocumentManager.MdiActiveDocument);
			RefreshBlockList();
			SyncSelectionFromDrawing();

			this.UpdateLayout(); 
		}

		private void BlocksIoPalette_Unloaded(object sender, RoutedEventArgs e)
		{
			AcApp.DocumentManager.DocumentActivated -= DocumentManager_DocumentActivated;
			DetachFromDocument();
		}

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

		private void Doc_ImpliedSelectionChanged(object? sender, EventArgs e)
		{
			SyncSelectionFromDrawing();
		}

		/// <summary>
		/// Обновляет отметки «в выделении» и счётчики. НЕ трогает пользовательские галочки
		/// и не навязывает фильтр списка — список всегда показывает все определения блоков.
		/// </summary>
		private void SyncSelectionFromDrawing()
		{
			var doc = AcApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			int selectedBlockCount = 0;
			var selectedTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				var pickfirst = doc.Editor.SelectImplied();
				if (pickfirst.Status == PromptStatus.OK && pickfirst.Value != null)
				{
					using var tr = doc.TransactionManager.StartTransaction();
					foreach (SelectedObject selObj in pickfirst.Value)
					{
						if (selObj == null) continue;
						if (tr.GetObject(selObj.ObjectId, OpenMode.ForRead) is not BlockReference blockRef) continue;

						selectedTypeNames.Add(GetRealBlockName(blockRef, tr));
						selectedBlockCount++;
					}
				}
			}
			catch (Exception ex)
			{
				SetStatus($"Не удалось прочитать текущее выделение: {ex.Message}", isError: true);
				return;
			}

			foreach (var item in BlockItems)
				item.IsInSelection = selectedTypeNames.Contains(item.BlockName);

			_lastSelectedBlockCount = selectedBlockCount;
			_lastSelectedTypeCount = selectedTypeNames.Count;

			_blockItemsView.Refresh();
			UpdateCountsStatus();
		}

		private static string GetRealBlockName(BlockReference blockRef, Transaction tr)
		{
			if (!blockRef.IsDynamicBlock)
				return blockRef.Name;

			var dynBtr = (BlockTableRecord)tr.GetObject(blockRef.DynamicBlockTableRecord, OpenMode.ForRead);
			return dynBtr.Name;
		}

		private void UpdateCountsStatus()
		{
			int checkedCount = BlockItems.Count(b => b.IsSelected);
			int exportableTypes = BlockItems.Count(b => b.IsSelected && b.IsInSelection);

			string text = _lastSelectedBlockCount == 0
				? $"В выделении: 0 блоков. Отмечено типов: {checkedCount}. Выделите блоки в чертеже для экспорта."
				: $"В выделении: {_lastSelectedBlockCount} блоков / {_lastSelectedTypeCount} типов. " +
				  $"Отмечено типов: {checkedCount}, к экспорту: {exportableTypes}.";

			CountsLabel.Text = text;
		}

		private void RefreshBlockList()
		{
			var doc = AcApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			// Сохраняем отметки пользователя по имени между обновлениями.
			var previouslyChecked = BlockItems
				.Where(b => b.IsSelected)
				.Select(b => b.BlockName)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			BlockItems.Clear();

			try
			{
				using var tr = doc.TransactionManager.StartTransaction();
				var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);

				foreach (ObjectId btrId in blockTable)
				{
					var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

					if (btr.IsLayout || btr.Name.StartsWith("*") || btr.Name.StartsWith("_"))
						continue;

					var item = new BlockSelectionItem
					{
						BlockName = btr.Name,
						IsSelected = previouslyChecked.Contains(btr.Name),
						InstanceCount = CountInstances(btr),
						IsDynamic = SafeIsDynamic(btr),
						HasAttributes = SafeHasAttributes(btr)
					};
					item.PropertyChanged += Item_PropertyChanged;
					BlockItems.Add(item);
				}

				_blockItemsView.Refresh();
			}
			catch (Exception ex)
			{
				SetStatus($"Ошибка загрузки списка: {ex.Message}", isError: true);
			}
		}

		private static int CountInstances(BlockTableRecord btr)
		{
			try
			{
				var ids = btr.GetBlockReferenceIds(true, true);
				return ids?.Count ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		private static bool SafeIsDynamic(BlockTableRecord btr)
		{
			try { return btr.IsDynamicBlock; }
			catch { return false; }
		}

		private static bool SafeHasAttributes(BlockTableRecord btr)
		{
			try { return btr.HasAttributeDefinitions; }
			catch { return false; }
		}

		private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(BlockSelectionItem.IsSelected))
				UpdateCountsStatus();
		}

		private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			var text = SearchBox.Text?.Trim() ?? string.Empty;
			if (string.IsNullOrEmpty(text))
			{
				_blockItemsView.Filter = null;
			}
			else
			{
				_blockItemsView.Filter = obj =>
					obj is BlockSelectionItem item &&
					item.BlockName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
			}
			_blockItemsView.Refresh();
		}

		private void InvertBtn_Click(object sender, RoutedEventArgs e)
		{
			// Инвертируем только то, что сейчас видно (с учетом поиска)
			foreach (var item in _blockItemsView.Cast<BlockSelectionItem>())
			{
				item.IsSelected = !item.IsSelected;
			}
		}

		private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
		{
			foreach (var item in _blockItemsView.Cast<BlockSelectionItem>())
				item.IsSelected = true;
		}

		private void ClearAllBtn_Click(object sender, RoutedEventArgs e)
		{
			foreach (var item in _blockItemsView.Cast<BlockSelectionItem>())
				item.IsSelected = false;
		}

		private void RefreshBtn_Click(object sender, RoutedEventArgs e)
		{
			RefreshBlockList();
			SyncSelectionFromDrawing();
			SetStatus("Список блоков обновлён.", isError: false);
		}

		private void ExportBtn_Click(object sender, RoutedEventArgs e)
		{
			var doc = AcApp.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			var selection = doc.Editor.SelectImplied();
			if (selection.Status != PromptStatus.OK || selection.Value == null || selection.Value.Count == 0)
			{
				SetStatus("Нет выделенных объектов. Выделите блоки в чертеже.", isError: true);
				return;
			}

			var selectedTypes = BlockItems
				.Where(b => b.IsSelected)
				.Select(b => b.BlockName)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			if (selectedTypes.Count == 0)
			{
				SetStatus("Не отмечено ни одного типа блока для экспорта.", isError: true);
				return;
			}

			// Извлекаем ДО диалога сохранения
			List<BlockData> blocks;
			try
			{
				blocks = BlockDataExtractor.Extract(doc, selection.Value.GetObjectIds(), selectedTypes);
			}
			catch (Exception ex)
			{
				SetStatus($"Ошибка извлечения данных: {ex.Message}", isError: true);
				return;
			}

			if (blocks.Count == 0)
			{
				SetStatus("Среди выделенных объектов нет блоков отмеченных типов.", isError: true);
				return;
			}

			var saveDialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Excel Files (*.xlsx)|*.xlsx",
				DefaultExt = "xlsx",
				FileName = BuildDefaultExportFileName(doc)
			};

			if (saveDialog.ShowDialog() != true) return;

			try
			{
				ExcelExporter.Export(blocks, saveDialog.FileName);
				SetStatus(
					$"Экспортировано {blocks.Count} вхождений в {Path.GetFileName(saveDialog.FileName)}.",
					isError: false);
			}
			catch (Exception ex)
			{
				SetStatus($"Ошибка экспорта: {ex.Message}", isError: true);
			}
		}

		private static string BuildDefaultExportFileName(Document doc)
		{
			string baseName;
			try
			{
				baseName = string.IsNullOrWhiteSpace(doc.Name)
					? "blocks"
					: Path.GetFileNameWithoutExtension(doc.Name);
			}
			catch
			{
				baseName = "blocks";
			}
			return $"{baseName}_blocks_{DateTime.Now:yyyyMMdd}.xlsx";
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

			// Предпросмотр содержимого файла до применения
			(int rows, int attrCols, int dynCols) preview;
			try
			{
				preview = ExcelImporter.Preview(openDialog.FileName);
			}
			catch (Exception ex)
			{
				SetStatus($"Не удалось прочитать файл: {ex.Message}", isError: true);
				return;
			}

			// Используем System.Windows.MessageBox из-за конфликта с WinForms
			var confirm = System.Windows.MessageBox.Show(
				$"Файл: {Path.GetFileName(openDialog.FileName)}\n" +
				$"Строк данных: {preview.rows}\n" +
				$"Столбцов атрибутов: {preview.attrCols}\n" +
				$"Столбцов динамических свойств: {preview.dynCols}\n\n" +
				"Применить значения к блокам в чертеже?\n" +
				"Изменения можно отменить командой UNDO.",
				"Импорт из Excel",
				MessageBoxButton.OKCancel,
				MessageBoxImage.Question);

			if (confirm != MessageBoxResult.OK) return;

			try
			{
				ImportResult result = ExcelImporter.Import(doc, openDialog.FileName);
				SetStatus(FormatImportResult(result),
					isError: result.ErrorCount > 0 || result.NotFoundCount > 0);
				SyncSelectionFromDrawing();
			}
			catch (Exception ex)
			{
				SetStatus($"Ошибка импорта: {ex.Message}", isError: true);
			}
		}

		private static string FormatImportResult(ImportResult r)
		{
			var summary = $"Импорт: обновлено {r.UpdatedCount}";
			if (r.NotFoundCount > 0) summary += $", не найдено {r.NotFoundCount}";
			if (r.ErrorCount > 0) summary += $", ошибок {r.ErrorCount}";
			summary += $" (всего строк: {r.TotalRowsProcessed}).";

			if (r.Errors.Count > 0)
			{
				summary += " " + string.Join(" | ", r.Errors.Take(3));
				if (r.Errors.Count > 3)
					summary += $" … (+{r.Errors.Count - 3})";
			}
			return summary;
		}

		private void SetStatus(string message, bool isError)
		{
			StatusLabel.Text = message;
			if (isError)
				StatusLabel.Foreground = System.Windows.Media.Brushes.Firebrick; // Явно указываем WPF Brushes
			else
				StatusLabel.ClearValue(TextBlock.ForegroundProperty);

			// Дублируем ошибки в командную строку как вторичный канал.
			if (isError)
			{
				var doc = AcApp.DocumentManager.MdiActiveDocument;
				doc?.Editor?.WriteMessage($"\n[Blocks IO] {message}");
			}
		}
	}
}
