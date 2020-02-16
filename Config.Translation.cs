using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace PollBot {

    public partial class Config {

        public class Translation {

            [Required, YamlMember(Alias = "approve")]
            public string Approve { get; set; } = "approve";

            [Required, YamlMember(Alias = "reject")]
            public string Reject { get; set; } = "reject";

            [Required, YamlMember(Alias = "approved")]
            public string Approved { get; set; } = "approved";

            [Required, YamlMember(Alias = "rejected")]
            public string Rejected { get; set; } = "rejected";

            [Required, YamlMember(Alias = "reply")]
            public string Reply { get; set; } = "reply";

            [Required, YamlMember(Alias = "stats")]
            public string Stats { get; set; } = "stats";

            [Required, YamlMember(Alias = "help")]
            public string Help { get; set; } = "help";

            [Required, YamlMember(Alias = "error-format")]
            public string FormatError { get; set; } = "error-format";

            [Required, YamlMember(Alias = "error-disallow")]
            public string DisallowError { get; set; } = "error-disallow";

            [Required, YamlMember(Alias = "error-reject")]
            public string RejectError { get; set; } = "error-reject";

            [Required, YamlMember(Alias = "error-changed")]
            public string HashMisMatchError { get; set; } = "error-changed";

            [Required, YamlMember(Alias = "error-permission")]
            public string PermissionError { get; set; } = "error-permission";

            [Required, YamlMember(Alias = "error-exception")]
            public string ExceptionError { get; set; } = "error-exception";
        }
    }
}