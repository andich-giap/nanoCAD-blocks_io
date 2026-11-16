// excel_importer.cs
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;

namespace NanoCadIo.BlockIO
{
	public static class ExcelImporter
	{
		/// <summary>
		/// Быстрый предпросмотр файла: количество строк данных и столбцов Attr_/Dyn_.
		/// Используется в панели для подтверждения импорта.
		/// </summary>
		public static (int rows, int attrCols, int dynCols) Preview(string filePath)
		{
			using var workbook = new XLWorkbook(filePath);
			var ws = workbook.Worksheet(1);

			var lastColUsed = ws.LastColumnUsed();
			var lastRowUsed = ws.LastRowUsed();
			if (lastColUsed == null || lastRowUsed == null)
				return (0, 0, 0);

			int lastCol = lastColUsed.ColumnNumber();
			int attrCols = 0, dynCols = 0;
			for (int c = 1; c <= lastCol; c++)
			{
				var header = ws.Cell(1, c).GetString();
				if (header.StartsWith("Attr_", StringComparison.OrdinalIgnoreCase)) attrCols++;
				else if (header.StartsWith("Dyn_", StringComparison.OrdinalIgnoreCase)) dynCols++;
			}

			return (Math.Max(0, lastRowUsed.RowNumber() - 1), attrCols, dynCols);
		}

		/// <summary>
		/// Обновляет блоки в чертеже данными из Excel.
		/// Изменения вносятся под блокировкой документа в рамках одной транзакции
		/// Document.TransactionManager, поэтому Commit попадает в undo-стек
		/// редактора (UNDO / REDO работают как для обычной команды).
		/// Возвращает детальный результат: обновлено / не найдено / ошибки.
		/// </summary>
		public static ImportResult Import(Document doc, string filePath)
		{
			var result = new ImportResult();

			using var workbook = new XLWorkbook(filePath);
			var ws = workbook.Worksheet(1);

			var lastColUsed = ws.LastColumnUsed();
			var lastRowUsed = ws.LastRowUsed();
			if (lastColUsed == null || lastRowUsed == null)
				return result;

			int lastCol = lastColUsed.ColumnNumber();
			int lastRow = lastRowUsed.RowNumber();

			// Читаем заголовки
			var headers = new Dictionary<int, string>();
			for (int c = 1; c <= lastCol; c++)
				headers[c] = ws.Cell(1, c).GetString();

			// Валидация: файл должен содержать столбец Handle
			bool hasHandleColumn = headers.Values
				.Any(h => h.Equals("Handle", StringComparison.OrdinalIgnoreCase));
			if (!hasHandleColumn)
			{
				throw new InvalidOperationException(
					"Файл не содержит столбца 'Handle'. Выберите файл, созданный этой панелью.");
			}

			// Читаем строки данных
			var rows = new List<Dictionary<string, string>>();
			for (int r = 2; r <= lastRow; r++)
			{
				var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				for (int c = 1; c <= lastCol; c++)
					rowData[headers[c]] = ws.Cell(r, c).GetString();
				rows.Add(rowData);
			}

			var db = doc.Database;

			// Блокировка документа обязательна при вызове из немодальной панели,
			// иначе UpgradeOpen бросит eLockViolation и изменения не применятся.
			// Транзакция стартует через Document.TransactionManager — её Commit
			// автоматически добавляется в undo-стек документа.
			using (doc.LockDocument())
			using (var tr = doc.TransactionManager.StartTransaction())
			{
				var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
				var modelSpaceId = blockTable[BlockTableRecord.ModelSpace];

				// Собираем ID всех пространств (Модель + Листы)
				var spaceIds = new List<ObjectId> { modelSpaceId };
				var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
				foreach (DBDictionaryEntry entry in layoutDict)
				{
					var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
					if (layout.BlockTableRecordId != modelSpaceId)
						spaceIds.Add(layout.BlockTableRecordId);
				}

				// Индекс блоков по Handle для быстрого поиска
				var blockRefByHandle = new Dictionary<string, BlockReference>();
				foreach (var spaceId in spaceIds)
				{
					var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
					foreach (var objId in space)
					{
						if (tr.GetObject(objId, OpenMode.ForRead) is BlockReference br)
							blockRefByHandle[br.Handle.ToString()] = br;
					}
				}

				for (int i = 0; i < rows.Count; i++)
				{
					var row = rows[i];
					int rowNumber = i + 2; // заголовок в строке 1

					if (!row.TryGetValue("Handle", out var handleStr) || string.IsNullOrWhiteSpace(handleStr))
					{
						result.ErrorCount++;
						result.Errors.Add($"Строка {rowNumber}: отсутствует значение в столбце Handle.");
						continue;
					}

					if (!blockRefByHandle.TryGetValue(handleStr, out var blockRef))
					{
						result.NotFoundCount++;
						continue;
					}

					// Открываем блок для записи
					blockRef.UpgradeOpen();
					bool rowHadError = false;

					// Обновляем атрибуты
					foreach (ObjectId attId in blockRef.AttributeCollection)
					{
						if (tr.GetObject(attId, OpenMode.ForWrite) is not AttributeReference attRef)
							continue;

						var headerKey = $"Attr_{attRef.Tag}";
						if (row.TryGetValue(headerKey, out var newValue))
							attRef.TextString = newValue;
					}

					// Обновляем динамические свойства
					if (blockRef.IsDynamicBlock)
					{
						var dynProps = blockRef.DynamicBlockReferencePropertyCollection;
						foreach (DynamicBlockReferenceProperty prop in dynProps)
						{
							if (prop == null || !prop.Show || prop.ReadOnly)
								continue;

							var headerKey = $"Dyn_{prop.PropertyName}";
							if (!row.TryGetValue(headerKey, out var newValue))
								continue;

							try
							{
								object valueToSet = ConvertValueForProperty(prop, newValue);
								prop.Value = valueToSet;
							}
							catch (Exception ex)
							{
								rowHadError = true;
								result.Errors.Add(
									$"Блок {handleStr}, свойство '{prop.PropertyName}': {ex.Message}");
							}
						}
					}

					if (rowHadError)
						result.ErrorCount++;
					else
						result.UpdatedCount++;
				}

				tr.Commit();
			}

			return result;
		}

		private static object ConvertValueForProperty(DynamicBlockReferenceProperty prop, string rawValue)
		{
			if (string.IsNullOrWhiteSpace(rawValue))
				return rawValue;

			// Если свойство принимает список значений (перечисление) — возвращаем строку
			var allowed = prop.GetAllowedValues();
			if (allowed != null && allowed.Length > 0)
				return rawValue;

			// Пытаемся привести к типу текущего значения
			var currentType = prop.Value?.GetType();
			if (currentType == typeof(double) || currentType == typeof(float) || currentType == typeof(decimal))
				return double.Parse(rawValue, CultureInfo.InvariantCulture);
			if (currentType == typeof(int) || currentType == typeof(long))
				return int.Parse(rawValue);
			if (currentType == typeof(bool))
				return bool.Parse(rawValue);

			return rawValue;
		}
	}
}
