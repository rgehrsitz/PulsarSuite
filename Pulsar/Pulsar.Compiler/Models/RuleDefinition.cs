// File: Pulsar.Compiler/Models/RuleDefinition.cs

using System.Text.Json.Serialization;
using Pulsar.Compiler.Exceptions;
using Serilog;

namespace Pulsar.Compiler.Models
{
    public class RuleDefinition
    {
        private static readonly ILogger _logger = LoggingConfig.GetLogger();

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ConditionGroup? Conditions { get; set; }
        public List<ActionDefinition> Actions { get; set; } = new();
        public List<InputDefinition> Inputs { get; set; } = new();
        public ElseClause? Else { get; set; }
        public string SourceFile { get; set; } = string.Empty;
        public int LineNumber { get; set; }

        /// <summary>
        /// List of input sensors referenced by this rule's conditions.
        /// </summary>
        public List<string> InputSensors { get; set; } = new List<string>();

        /// <summary>
        /// List of output sensors written by this rule's actions.
        /// </summary>
        public List<string> OutputSensors { get; set; } = new List<string>();

        /// <summary>
        /// Get all actions including else actions for processing
        /// </summary>
        public List<ActionDefinition> ElseActions => Else?.Actions ?? new List<ActionDefinition>();

        public void Validate()
        {
            try
            {
                _logger.Debug("Validating rule: {RuleName}", Name);

                if (string.IsNullOrEmpty(Name))
                {
                    _logger.Error("Rule name is required");
                    throw new ValidationException("Rule name is required");
                }

                if (Conditions == null)
                {
                    _logger.Error("Rule {RuleName} must have conditions", Name);
                    throw new ValidationException($"Rule {Name} must have conditions");
                }

                if (Actions.Count == 0)
                {
                    _logger.Error("Rule {RuleName} must have at least one action", Name);
                    throw new ValidationException($"Rule {Name} must have at least one action");
                }

                // Validate conditions
                Conditions.Validate();

                // Validate actions
                foreach (var action in Actions)
                {
                    action.Validate();
                }

                _logger.Debug("Rule {RuleName} validation successful", Name);
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                _logger.Error(ex, "Rule {RuleName} validation failed", Name);
                throw new ValidationException($"Rule {Name} validation failed: {ex.Message}", ex);
            }
        }

        public void LogInfo()
        {
            _logger.Information(
                "Rule: {Name} from {File}:{Line}, {ActionCount} actions",
                Name,
                SourceFile,
                LineNumber,
                Actions.Count
            );
        }
    }

    public class ConditionGroup : ConditionDefinition
    {
        public new ConditionType Type { get; set; } = ConditionType.Group;
        public List<ConditionDefinition> All { get; set; } = new();
        public List<ConditionDefinition> Any { get; set; } = new();
        public ConditionGroup? Parent { get; private set; }

        public void AddToAll(ConditionDefinition condition)
        {
            if (condition is ConditionGroup group)
            {
                group.Parent = this;
            }
            All.Add(condition);
        }

        public void AddToAny(ConditionDefinition condition)
        {
            if (condition is ConditionGroup group)
            {
                group.Parent = this;
            }
            Any.Add(condition);
        }

        public override void Validate()
        {
            _logger.Debug("Validating condition group");
            if ((All == null || All.Count == 0) && (Any == null || Any.Count == 0))
            {
                _logger.Error("Condition group must have at least one condition in All or Any");
                throw new ValidationException(
                    "Condition group must have at least one condition in All or Any"
                );
            }

            foreach (var condition in All ?? Enumerable.Empty<ConditionDefinition>())
            {
                condition.Validate();
            }

            foreach (var condition in Any)
            {
                condition.Validate();
            }
        }
    }

    public class RuleGroup
    {
        public List<RuleDefinition> Rules { get; set; } = new();
    }

    public abstract class ConditionDefinition
    {
        [JsonIgnore]
        protected static readonly ILogger _logger = LoggingConfig.GetLogger();

        public ConditionType Type { get; set; }
        public SourceInfo? SourceInfo { get; set; }

        public abstract void Validate();
    }

    public enum ConditionType
    {
        Comparison,
        Expression,
        ThresholdOverTime,
        Group,
    }

    public class ComparisonCondition : ConditionDefinition
    {
        public new ConditionType Type { get; set; } = ConditionType.Comparison;
        public string Sensor { get; set; } = string.Empty;
        public ComparisonOperator Operator { get; set; }
        public object? Value { get; set; }  // Changed from double to object? to support boolean values

        public override void Validate()
        {
            _logger.Debug("Validating comparison condition for sensor {Sensor}", Sensor);
            if (string.IsNullOrEmpty(Sensor))
            {
                _logger.Error("Comparison condition must specify a sensor");
                throw new ValidationException("Sensor is required for comparison condition");
            }
        }
    }

    public enum ComparisonOperator
    {
        LessThan,
        GreaterThan,
        LessThanOrEqual,
        GreaterThanOrEqual,
        EqualTo,
        NotEqualTo,
    }

    public class ExpressionCondition : ConditionDefinition
    {
        public new ConditionType Type { get; set; } = ConditionType.Expression;
        public string Expression { get; set; } = string.Empty;

        public override void Validate()
        {
            _logger.Debug("Validating expression condition: {Expression}", Expression);
            if (string.IsNullOrEmpty(Expression))
            {
                _logger.Error("Expression condition must have an expression");
                throw new ValidationException("Expression is required");
            }
        }
    }

    public enum ThresholdOverTimeMode
    {
        Strict, // Only trust explicit data points
        Extended, // Use last known value for missing points
    }

    public class ThresholdOverTimeCondition : ConditionDefinition
    {
        public new ConditionType Type { get; set; } = ConditionType.ThresholdOverTime;
        public string Sensor { get; set; } = string.Empty;
        public double Threshold { get; set; }
        public int Duration { get; set; }
        public ThresholdOverTimeMode Mode { get; set; } = ThresholdOverTimeMode.Strict;
        public ComparisonOperator ComparisonOperator { get; set; } = ComparisonOperator.GreaterThan;

        public override void Validate()
        {
            if (string.IsNullOrEmpty(Sensor))
            {
                _logger.Error("Sensor is required for threshold over time condition");
                throw new ValidationException(
                    "Sensor is required for threshold over time condition"
                );
            }

            if (Duration <= 0)
            {
                _logger.Error("Duration must be greater than 0");
                throw new ValidationException("Duration must be greater than 0");
            }
        }
    }

    public abstract class ActionDefinition
    {
        [JsonIgnore]
        protected static readonly ILogger _logger = LoggingConfig.GetLogger();

        public ActionType Type { get; set; }
        public SourceInfo? SourceInfo { get; set; }

        public virtual void Validate()
        {
            _logger.Debug("Validating action of type {Type}", Type);
        }
    }

    public enum ActionType
    {
        SetValue,
        SendMessage,
    }

    public class SetValueAction : ActionDefinition
    {
        public new ActionType Type { get; set; } = ActionType.SetValue;
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        public string? ValueExpression { get; set; }

        public override void Validate()
        {
            base.Validate();
            _logger.Debug("Validating SetValue action for key {Key}", Key);
            if (string.IsNullOrEmpty(Key))
            {
                _logger.Error("SetValue action must specify a key");
                throw new ValidationException("Key is required for SetValue action");
            }
            if (string.IsNullOrEmpty(ValueExpression) && Value == null)
            {
                _logger.Error("SetValue action must specify either Value or ValueExpression");
                throw new ValidationException("Either Value or ValueExpression must be specified");
            }
        }
    }

    public class SendMessageAction : ActionDefinition
    {
        public new ActionType Type { get; set; } = ActionType.SendMessage;
        public string Channel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? MessageExpression { get; set; }

        public override void Validate()
        {
            base.Validate();
            _logger.Debug("Validating SendMessage action for channel {Channel}", Channel);
            if (string.IsNullOrEmpty(Channel))
            {
                _logger.Error("SendMessage action must specify a channel");
                throw new ValidationException("Channel is required for SendMessage action");
            }
            
            // Allow either a static message or a dynamic message expression
            if (string.IsNullOrEmpty(Message) && string.IsNullOrEmpty(MessageExpression))
            {
                _logger.Error("SendMessage action must specify either Message or MessageExpression");
                throw new ValidationException("Either Message or MessageExpression must be specified for SendMessage action");
            }
        }
    }

    /// <summary>
    /// Input definition for v3 rules with fallback strategies
    /// </summary>
    public class InputDefinition
    {
        public string Id { get; set; } = string.Empty;
        public bool Required { get; set; } = true;
        public FallbackStrategy? Fallback { get; set; }
    }

    /// <summary>
    /// Fallback strategy for unavailable sensors
    /// </summary>
    public class FallbackStrategy
    {
        public string Strategy { get; set; } = string.Empty; // use_last_known, use_default, propagate_unavailable, skip_rule
        public string? MaxAge { get; set; }
        public object? DefaultValue { get; set; }
    }

    /// <summary>
    /// Else clause for v3 rules
    /// </summary>
    public class ElseClause
    {
        public List<ActionDefinition> Actions { get; set; } = new();
    }

    /// <summary>
    /// Emit control for v3 actions
    /// </summary>
    public enum EmitType
    {
        Always,
        OnChange,
        OnEnter
    }

    /// <summary>
    /// Base class for v3 action definitions with emit control
    /// </summary>
    public abstract class V3ActionDefinition : ActionDefinition
    {
        public EmitType Emit { get; set; } = EmitType.Always;
    }

    /// <summary>
    /// V3 Set action with emit control
    /// </summary>
    public class V3SetAction : V3ActionDefinition
    {
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        public string? ValueExpression { get; set; }

        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrEmpty(Key))
            {
                throw new ValidationException("Key is required for Set action");
            }
        }
    }

    /// <summary>
    /// V3 Log action with emit control
    /// </summary>
    public class V3LogAction : V3ActionDefinition
    {
        public string Log { get; set; } = string.Empty;

        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrEmpty(Log))
            {
                throw new ValidationException("Log message is required for Log action");
            }
        }
    }

    /// <summary>
    /// V3 Buffer action for temporal data collection
    /// </summary>
    public class V3BufferAction : V3ActionDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string? ValueExpression { get; set; }
        public int MaxItems { get; set; } = 100;

        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrEmpty(Key))
            {
                throw new ValidationException("Key is required for Buffer action");
            }
            if (MaxItems <= 0)
            {
                throw new ValidationException("MaxItems must be greater than 0");
            }
        }
    }
}
