public interface FileUploadService
    {
        public bool validateFile(IFormFile formFile);
        public Task<List<string>> GetColumns(IFormFile formFile);
        public Dictionary<string, List<string>> GetEntityProperties(EntityTypes entity);
        public void GetEntitiesForMapping(int parent,Dictionary<string, string> myDict,IFormFile file);
    }
