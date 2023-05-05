namespace RecAll.Contrib.MaskedTextList.Api.Models; 

public class MaskedTextItem {
    // 数据库主键
    public int Id { get; set; }
    //错题的题号
    public int? ItemId { get; set; }
    //题目的内容
    public string Content { get; set; }
    //隐藏的题目答案
    public string MaskedContent { get; set; }
    //用户识别标识符
    public string UserIdentityGuid { get; set; }
    //软删除标记
    public bool IsDeleted { get; set; }
}