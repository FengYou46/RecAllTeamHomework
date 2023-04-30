using System.ComponentModel.DataAnnotations;

namespace RecAll.Contrib.TextList.Api.Commands;

public class CreateTextItemCommand {
    [Required]
    public string Content { get; set; }
}