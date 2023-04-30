using System.ComponentModel.DataAnnotations;

namespace RecAll.Contrib.TextList.Api.Commands;

public class UpdateTextItemCommand {
    [Required] public int Id { get; set; }
    [Required] public string Content { get; set; }
}