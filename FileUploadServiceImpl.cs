    public class FileUploadServiceImpl : FileUploadService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly UserDbContext _context;
        //private readonly BaseRepository<T> _repo;
        public FileUploadServiceImpl(IServiceProvider serviceProvider, UserDbContext context)
        {
            _serviceProvider = serviceProvider;
            _context = context;
            //_repo = repo;
        }

        public bool validateFile(IFormFile formFile)
        {
            var isValid = true;

            if(formFile == null || formFile.Length <= 0)
            {
                throw new ItemNotFoundException($"File not found");
            }

            if (!Path.GetExtension(formFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                throw new ItemNotFoundException($"Not Support file extension");
            }

            return isValid;
        }

        public async Task<List<string>> GetColumns(IFormFile formFile)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    await formFile.CopyToAsync(stream);
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                    using (var package = new ExcelPackage(stream))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

                        if (worksheet.Dimension == null)
                            throw new InvalidDataException($"Column header not present");

                        List<string> ColumnNames = new List<string>();
                        for (int i = 1; i <= worksheet.Dimension.End.Column; i++)
                        {
                            if (worksheet.Cells[1, i].Value != null)
                                ColumnNames.Add(worksheet.Cells[1, i].Value.ToString());
                        }
                        return ColumnNames;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<Dictionary<string, string>>> GetRows(IFormFile formFile)
        {
            try
            {
                using (var stream = new MemoryStream())
                {
                    await formFile.CopyToAsync(stream);
                    ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                    using (var package = new ExcelPackage(stream))
                    {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

                        if (worksheet.Dimension == null)
                            throw new InvalidDataException($"Column header not present");

                        List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
                        for (int i = 2; i <= worksheet.Dimension.Rows; i++)
                        {
                            var dict = new Dictionary<string, string>();
                            for (int j = 1; j <= worksheet.Dimension.End.Column; j++)
                            {
                                if(worksheet.Cells[1, j].Value != null)
                                    dict[worksheet.Cells[1, j].Value.ToString()] = worksheet.Cells[i, j].Value?.ToString();
                            }
                            rows.Add(dict);
                        }
                        return rows;
                    }
                }
            }
            catch
            {
                throw;
            }
        }

        public Dictionary<string, List<string>> GetEntityProperties(EntityTypes entity)
        {

            try
            {
                var type = FindEntity(entity);

                Dictionary<string, List<string>> resObj = new Dictionary<string, List<string>>();
                PropertyInfo[] prop = type.GetProperties();
                

                var ent = _context.Model.FindEntityType(type);

                var reqField = ent.GetProperties().Where(pr => pr.PropertyInfo.CustomAttributes.Select(p => p.AttributeType.Name).Contains("RequiredAttribute")).Select(pr => pr.Name).ToList();
                var propNames = prop.Select(p => p.Name).Select(prp => { if (reqField.Contains(prp)) return prp = prp + "**"; else return prp; }).ToList();
                
                resObj[type.Name + "**"] = propNames;

                var keys = ent.GetForeignKeys().ToList();

                var foreignKeyType = new List<IEntityType>();

                foreach (var key in keys)
                {
                    foreignKeyType.Add(key.PrincipalEntityType);
                    var frProp = key.PrincipalEntityType.GetProperties().ToList();
                    var names = new List<string>();
                    foreach (var pr in frProp)
                    {
                        names.Add(pr.Name);
                    }
                    resObj[key.PrincipalEntityType.DisplayName()] = names;
                }

                return resObj;

            }
            catch
            {
                throw;
            }
           
        }

        public async void GetEntitiesForMapping(int parent, Dictionary<string, string> myDict, IFormFile file)
        {
            try
            {
                Type parentType = FindEntity((EntityTypes)parent);

                var parentEntity = _context.Model.FindEntityType(parentType);
                var foreignKeys = parentEntity.GetForeignKeys().ToList();

                var allDataRowList = await GetRows(file);

                // myDist = <"item name", "item_name">
                var keys = myDict.Keys.ToList();

                allDataRowList.ForEach(pr =>
                {
                    var ks = pr.Keys.ToList();
                    ks.ForEach(pp => { if (!keys.Contains(pp)) pr.Remove(pp); });
                });

                foreach (string key in keys)
                {
                    if (parentType.GetProperties().Select(pr => pr.Name).Contains(myDict[key]))
                    {
                        allDataRowList.ForEach(dict => { 
                            var val = dict[key];
                            dict.Remove(key);
                            dict.Add(myDict[key], val);
                        });
                    }
                    else
                    {
                        var forgnKey = foreignKeys.Select(fr => fr.PrincipalEntityType).Where(fl => fl.FindProperty(myDict[key]) != null).FirstOrDefault();
                        var tableFrProp = foreignKeys.Where(fl => fl.PrincipalEntityType.FindProperty(myDict[key]) != null).Select(fl => fl.Properties[0].Name).FirstOrDefault();
                        if (forgnKey != null)
                        {
                            var field = forgnKey.FindProperty(myDict[key]).Name;
                            var id = forgnKey.FindPrimaryKey()?.Properties[0].Name;

                            var entType = forgnKey.ClrType;
                            var enty = Activator.CreateInstance(entType);

                            var qur = getListMethod(entType);
                            var dictPropToId = new Dictionary<string, int>();
                            foreach (object qu in qur)
                            {
                                dictPropToId[qu.GetType().GetProperty(field).GetValue(qu).ToString()] = Convert.ToInt32(qu.GetType().GetProperty(id).GetValue(qu).ToString());
                            }

                            allDataRowList.ForEach(dict => {
                                var val = dict[key];
                                dict.Remove(key);
                                if (!dictPropToId.ContainsKey(val))
                                    throw new CustomException($"No data {val} in column {key}");
                                dict.Add(tableFrProp, dictPropToId[val].ToString());
                            });
                        }
                    }
                }

                
                saveListMethod(parentType, allDataRowList);
                return;

            }
            catch (Exception e)
            {
                throw;
            }
        }


        private IList getListMethod(Type entType)
        {
            Type openRepoClass = typeof(BaseRepositoryImpl<>);
            Type closeRepoClass = openRepoClass.MakeGenericType(entType);
            object o = Activator.CreateInstance(closeRepoClass, _context);

            var rv = closeRepoClass.InvokeMember("getAll", BindingFlags.InvokeMethod, null, o, new object[0]);

            return (IList)rv;
           
        }

        private void saveListMethod(Type entType, List<Dictionary<string, string>> dataObj)
        {
            try
            {
                Type openRepoClass = typeof(BaseRepositoryImpl<>);
                Type closeRepoClass = openRepoClass.MakeGenericType(entType);
                object o = Activator.CreateInstance(closeRepoClass, _context);

                foreach(var obj in dataObj)
                {
                    var entObj = Activator.CreateInstance(entType);

                    obj.Keys.ToList().ForEach(pr =>
                    {
                        PropertyInfo piInstance = entType.GetProperty(pr);
                        var type = piInstance.PropertyType;
                        var tp = type.IsGenericType && type.GetGenericTypeDefinition().Equals(typeof(Nullable<>))
                        ? Nullable.GetUnderlyingType(piInstance.PropertyType)
                        : type ;
                        piInstance.SetValue(entObj, Convert.ChangeType(obj[pr], tp));
                    });

                    PropertyInfo piInstance = entType.GetProperty("ORGANISATION_ID");
                    piInstance.SetValue(entObj, 1);

                    var rv = closeRepoClass.InvokeMember("insert", BindingFlags.InvokeMethod, null, o, new object[1] { entObj });
                }

                return;
            }
            catch
            {
                throw;
            }
        }

        private static Type FindEntity(EntityTypes entity)
        {
            try
            {
                Type type;

                switch (entity)
                {
                    case EntityTypes.item:
                        type = typeof(ITEM);
                        break;
                    case EntityTypes.unit:
                        type = typeof(UNIT);
                        break;
                    case EntityTypes.item_category:
                        type = typeof(ITEM_CATEGORY);
                        break;
                    default:
                        throw new ItemNotFoundException($"Table not found for {entity.GetDisplayName()}.");
                }

                return type;
            }
            catch
            {
                throw;
            }
        }
    
