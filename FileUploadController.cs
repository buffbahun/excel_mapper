
    [Route("api/import")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    public class FileUploadController : BaseController
    {
        private readonly FileUploadService _uploadService;
        public FileUploadController(FileUploadService uploadService)
        {
            _uploadService = uploadService;
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("types")]
        public IActionResult getEntityType()
        {
            try
            {
                var enumList = new List<Object>();
                foreach (EntityTypes data in Enum.GetValues(typeof(EntityTypes)))
                {
                    enumList.Add(new {key = (int)data, name = data.GetDisplayName()});
                }
                return Ok(enumList);
            }
            catch (Exception ex)
            {
                return Ok(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("file_header")]
        public async Task<object> GetExcelHeaders(IFormFile file)
        {
            try
            {

                var isValid = _uploadService.validateFile(file);
                
                if (isValid)
                {
                    var columnList = await _uploadService.GetColumns(file);
                    return Ok(columnList);
                }
                else
                {
                    return BadRequest();
                }
                
            }
            catch (CustomException e)
            {
                return ApiResponse.getErrorResponseJson(e.Message);
            }
            catch (Exception e)
            {
                return ApiResponse.getErrorResponseJson(e.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("db_header")]
        public IActionResult getDbHeaders([FromQuery] EntityTypes type)
        {
            try
            {
                var headerList = _uploadService.GetEntityProperties(type);
                return Ok(headerList);
            }
            catch (CustomException e)
            {
                return ApiResponse.getErrorResponseJson(e.Message);
            }
            catch (Exception e)
            {
                return ApiResponse.getErrorResponseJson(e.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("save-table")]
        //[
        //    {isParent: true, item: {}},
        //    { unit: { } },
        //    { category: { } }
        //]
        public IActionResult saveTable([FromForm] IFormFile file, [FromForm] string jsonString, [FromQuery] int parent)
        {
            try
            {
                var myDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);

                _uploadService.GetEntitiesForMapping(parent, myDict, file);

                return Ok();
            }
            catch (Exception e)
            {
                return Ok(e.Message);
            }
        }


    }

    public class fileUploadDto
    {
        public IFormFile file;
        public string jsonString;
    }
