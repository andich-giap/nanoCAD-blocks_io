using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;

namespace NanoCadIO.BlockIO
{
	public static class ExcelImporter
	{
		/// <summary>
		/// Обновляет блоки в чертеже данными из Excel.
		/// Возвращает количество успешно обновлённых блоков.
		/// </summary>
		public static int Import(Document doc, string filePath)
		{
			using var workbook = new XLWorkbook(filePath);
			var ws = workbook.Worksheet(1);

			// Читаем заголовки, чтобы понять, какие столбцы к чему относятся
			var headers = new Dictionary<int, string>();
			int lastCol = ws.LastColumnUsed()!.ColumnNumber();
			for (int c = 1; c <= lastCol; c++)
			{
				headers[c] = ws.Cell(1, c).GetString();
			}

			// Собираем строки данных
			var rows = new List<Dictionary<string, string>>();
			int lastRow = ws.LastRowUsed()!.RowNumber();
			for (int r = 2; r <= lastRow; r++)
			{
				var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				for (int c = 1; c <= lastCol; c++)
				{
					rowData[headers[c]] = ws.Cell(r, c).GetString();
				}
				rows.Add(rowData);
			}

			var db = doc.Database;
			int updatedCount = 0;

			using var tr = db.TransactionManager.StartTransaction();
			
			// Получаем таблицу блоков и ID пространства модели
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

			// Строим индекс блоков по Handle для быстрого поиска во всем чертеже
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

			foreach (var row in rows)
			{
				if (!row.TryGetValue("Handle", out var handleStr))
					continue;

				if (!blockRefByHandle.TryGetValue(handleStr, out var blockRef))
					continue; // Блок не найден во всем чертеже

				// Открываем блок для записи
				blockRef.UpgradeOpen();

				// Обновляем атрибуты
				foreach (ObjectId attId in blockRef.AttributeCollection)
				{
					if (tr.GetObject(attId, OpenMode.ForWrite) is not AttributeReference attRef)
						continue;

					var headerKey = $"Attr_{attRef.Tag}";
					if (row.TryGetValue(headerKey, out var newValue))
					{
						attRef.TextString = newValue;
					}
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
							// Приводим строку к типу, который ожидает свойство
							object valueToSet = ConvertValueForProperty(prop, newValue);
							prop.Value = valueToSet;
						}
						catch (Exception ex)
						{
							// Можно логировать ошибку, но не прерывать весь импорт
							doc.Editor.WriteMessage($"\nНе удалось установить свойство {prop.PropertyName} для блока {handleStr}: {ex.Message}");
						}
					}
				}

				updatedCount++;
			}

			tr.Commit();
			return updatedCount;
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
				return double.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);
			if (currentType == typeof(int) || currentType == typeof(long))
				return int.Parse(rawValue);
			if (currentType == typeof(bool))
				return bool.Parse(rawValue);

			return rawValue;
		}
	}
}
