using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace PollBot {

    public partial class Config {

        public class Translation {

            [Required, YamlMember(Alias = "approve")]
            public string Approve { get; set; } = "approve";

            [Required, YamlMember(Alias = "reject")]
            public string Reject { get; set; } = "reject";

            [Required, YamlMember(Alias = "duplicate")]
            public string Duplicate { get; set; } = "duplicate";

            [Required, YamlMember(Alias = "approved")]
            public string Approved { get; set; } = "approved";

            [Required, YamlMember(Alias = "rejected")]
            public string Rejected { get; set; } = "rejected";

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

            [Required, YamlMember(Alias = "error-noreply")]
            public string NoReplyError { get; set; } = "error-noreply";

            [Required, YamlMember(Alias = "error-notpoll")]
            public string NotPollError { get; set; } = "error-notpoll";

            [Required, YamlMember(Alias = "error-notclosed")]
            public string NotClosedError { get; set; } = "error-notclosed";

            [Required, YamlMember(Alias = "error-notquiz")]
            public string NotQuizError { get; set; } = "error-notquiz";

            [Required, YamlMember(Alias = "error-questiontoolong")]
            public string QuestionTooLongError { get; set; } = "error-questiontoolong";

            [Required, YamlMember(Alias = "error-optiontoolong")]
            public string OptionTooLongError { get; set; } = "error-optiontoolong";

            [Required, YamlMember(Alias = "error-wrongoptionsize")]
            public string WrongOptionSizeError { get; set; } = "error-wrongoptionsize";
        }
    }
}