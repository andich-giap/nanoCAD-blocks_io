using System.Collections.Generic;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;

namespace NanoCadIO.BlockIO
{
	public static class BlockDataExtractor
	{
		/// <summary>
		/// Собирает данные со всех блоков в указанном пространстве (модель или лист).
		/// Если blockName == null, собираются все блоки.
		/// </summary>
		public static List<BlockData> Extract(Document doc, string? blockName = null)
		{
			var result = new List<BlockData>();
			var db = doc.Database;

			using var tr = db.TransactionManager.StartTransaction();
			
			// Получаем текущее пространство (модель или активный лист)
			var spaceId = db.CurrentSpaceId;
			var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);

			foreach (var objId in space)
			{
				if (tr.GetObject(objId, OpenMode.ForRead) is not BlockReference blockRef)
					continue;

				if (blockName != null && !blockRef.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
					continue;

				var data = new BlockData
				{
					Handle = blockRef.Handle.ToString(),
					BlockName = blockRef.Name
				};

				// 1. Атрибуты
				foreach (ObjectId attId in blockRef.AttributeCollection)
				{
					if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
					{
						// Можно фильтровать невидимые, если нужно
						data.Attributes[attRef.Tag] = attRef.TextString;
					}
				}

				// 2. Динамические свойства
				if (blockRef.IsDynamicBlock)
				{
					var dynProps = blockRef.DynamicBlockReferencePropertyCollection;
					foreach (DynamicBlockReferenceProperty prop in dynProps)
					{
						if (prop == null || !prop.Show || prop.ReadOnly)
							continue;

						// Приводим значение к строке. Для массивов (например, списков) склеиваем.
						var val = prop.Value;
						string value = val switch
						{
							object[] arr => string.Join(", ", arr),
							null => string.Empty,
							_ => val.ToString() ?? string.Empty // ?? string.Empty убирает CS8600
						};
						data.DynamicProperties[prop.PropertyName] = value;
					}
				}

				result.Add(data);
			}
			tr.Commit();
			return result;
		}
	}
}
