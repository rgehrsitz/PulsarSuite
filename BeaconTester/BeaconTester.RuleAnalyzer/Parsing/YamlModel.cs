using YamlDotNet.Serialization;

namespace BeaconTester.RuleAnalyzer.Parsing
{
    // Direct copies of Pulsar's YAML parsing model classes
    /// <summary>
    /// Root object for YAML rules
    /// </summary>
    public class RuleRoot
    {
        public List<Rule> Rules { get; set; } = new List<Rule>();
    }

    /// <summary>
    /// A rule definition in YAML
    /// </summary>
    public class Rule
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConditionGroupYaml? Conditions { get; set; }
        public List<ActionItem>? Actions { get; set; }
        public List<InputItem>? Inputs { get; set; }
        
        // V3 else block support
        [YamlMember(Alias = "else")]
        public ElseBlock? Else { get; set; }
        
        // Line tracking
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// Condition group in YAML (all/any)
    /// </summary>
    public class ConditionGroupYaml
    {
        public List<ConditionItem>? All { get; set; }
        public List<ConditionItem>? Any { get; set; }
        
        // To detect invalid constructs like 'always: true'
        [YamlMember(Alias = "always")]
        public object? Always { get; set; }
    }

    /// <summary>
    /// Condition item in YAML (with condition wrapper or direct V3 format)
    /// </summary>
    public class ConditionItem
    {
        [YamlMember(Alias = "condition")]
        public ConditionDetails? Condition { get; set; }
        
        // V3 direct format support
        public string? Type { get; set; }
        public string? Sensor { get; set; }
        public string? Operator { get; set; }
        public object? Value { get; set; }
        public object? Threshold { get; set; }
        public string? Expression { get; set; }
        public object? Duration { get; set; }
        
        // V3 temporal condition support
        [YamlMember(Alias = "comparison_operator")]
        public string? ComparisonOperator { get; set; }
    }

    /// <summary>
    /// Condition details in YAML
    /// </summary>
    public class ConditionDetails
    {
        public string Type { get; set; } = string.Empty;
        public string? Sensor { get; set; }
        public string? Operator { get; set; }
        public object? Value { get; set; }
        [YamlMember(Alias = "threshold")]
        public object? Threshold { get; set; }
        public string? Expression { get; set; }
        public int? Duration { get; set; }
    }

    /// <summary>
    /// V3 else block
    /// </summary>
    public class ElseBlock
    {
        public List<ActionItem>? Actions { get; set; }
    }

    /// <summary>
    /// Action item in YAML
    /// </summary>
    public class ActionItem
    {
        // Legacy actions
        [YamlMember(Alias = "set_value")]
        public SetValueActionYaml? SetValue { get; set; }
        
        [YamlMember(Alias = "send_message")]
        public SendMessageActionYaml? SendMessage { get; set; }
        
        // V3 actions
        public V3SetAction? Set { get; set; }
        public V3LogAction? Log { get; set; }
        public V3BufferAction? Buffer { get; set; }
    }

    /// <summary>
    /// Set value action in YAML
    /// </summary>
    public class SetValueActionYaml
    {
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        
        [YamlMember(Alias = "value_expression")]
        public string? ValueExpression { get; set; }
    }

    /// <summary>
    /// Send message action in YAML
    /// </summary>
    public class SendMessageActionYaml
    {
        public string Channel { get; set; } = string.Empty;
        public string? Message { get; set; }
        
        [YamlMember(Alias = "message_expression")]
        public string? MessageExpression { get; set; }
    }

    /// <summary>
    /// Input item in YAML (with fallback/default)
    /// </summary>
    public class InputItem
    {
        [YamlMember(Alias = "id")]
        public string Id { get; set; } = string.Empty;
        [YamlMember(Alias = "required")]
        public bool? Required { get; set; }
        [YamlMember(Alias = "fallback")]
        public FallbackItem? Fallback { get; set; }
    }

    /// <summary>
    /// Fallback/default for an input
    /// </summary>
    public class FallbackItem
    {
        [YamlMember(Alias = "strategy")]
        public string? Strategy { get; set; }
        [YamlMember(Alias = "default_value")]
        public object? DefaultValue { get; set; }
        [YamlMember(Alias = "max_age")]
        public string? MaxAge { get; set; }
    }

}