using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 
public class Article
{
    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}
public class NewsResponse
{
    public string Location { get; set; }
    public List<Article> News { get; set; }
}
public class BookmarkRequest
{
    public string Url { get; set; }
}