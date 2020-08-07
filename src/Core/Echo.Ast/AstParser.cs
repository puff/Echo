using System;
using System.Collections.Generic;
using System.Linq;
using Echo.ControlFlow;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Regions;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.Core.Code;
using Echo.DataFlow;

namespace Echo.Ast
{
    /// <summary>
    /// Transforms a <see cref="ControlFlowGraph{TInstruction}"/> into an Ast
    /// </summary>
    public sealed class AstParser<TInstruction>
    {
        private readonly ControlFlowGraph<TInstruction> _controlFlowGraph;
        private readonly DataFlowGraph<TInstruction> _dataFlowGraph;
        private readonly IInstructionSetArchitecture<AstStatementBase<TInstruction>> _architecture;

        private readonly Dictionary<long, AstVariable[]> _stackSlots = new Dictionary<long, AstVariable[]>();
        private readonly Dictionary<BasicControlFlowRegion<TInstruction>, BasicControlFlowRegion<AstStatementBase<TInstruction>>>
            _regionsMapping = new Dictionary<BasicControlFlowRegion<TInstruction>, BasicControlFlowRegion<AstStatementBase<TInstruction>>>();

        private long _id = -1;
        private long _varCount;
        private long _phiVarCount;
        
        /// <summary>
        /// Creates a new Ast parser with the given <see cref="ControlFlowGraph{TInstruction}"/>
        /// </summary>
        /// <param name="controlFlowGraph">The <see cref="ControlFlowGraph{TInstruction}"/> to parse</param>
        /// <param name="dataFlowGraph">The <see cref="DataFlowGraph{TContents}"/> to parse</param>
        public AstParser(ControlFlowGraph<TInstruction> controlFlowGraph, DataFlowGraph<TInstruction> dataFlowGraph)
        {
            _controlFlowGraph = controlFlowGraph;
            _dataFlowGraph = dataFlowGraph;
            _architecture = new AstInstructionSetArchitectureDecorator<TInstruction>(_controlFlowGraph.Architecture);
        }

        private IInstructionSetArchitecture<TInstruction> Architecture => _controlFlowGraph.Architecture; 
        
        /// <summary>
        /// Parses the given <see cref="ControlFlowGraph{TInstruction}"/>
        /// </summary>
        /// <returns>A <see cref="CompilationUnit{TInstruction}"/> representing the Ast</returns>
        public CompilationUnit<TInstruction> Parse()
        {
            var newGraph = new CompilationUnit<TInstruction>(_architecture);
            var blockBuilder = new BlockBuilder<TInstruction>();
            var rootScope = blockBuilder.ConstructBlocks(_controlFlowGraph);

            // Transform and add regions.
            foreach (var originalRegion in _controlFlowGraph.Regions)
            {
                var newRegion = TransformRegion(originalRegion);
                newGraph.Regions.Add(newRegion);
            }

            // Transform and add nodes.
            foreach (var originalBlock in rootScope.GetAllBlocks())
            {
                var originalNode = _controlFlowGraph.Nodes[originalBlock.Offset];
                
                var transformedBlock = TransformBlock(originalBlock);
                var newNode = new ControlFlowNode<AstStatementBase<TInstruction>>(originalBlock.Offset, transformedBlock);
                newGraph.Nodes.Add(newNode);
                
                // Move node to newly created region.
                if (originalNode.ParentRegion is BasicControlFlowRegion<TInstruction> basicRegion)
                    newNode.MoveToRegion(_regionsMapping[basicRegion]);
            }

            // Clone edges.
            foreach (var originalEdge in _controlFlowGraph.GetEdges())
            {
                var newOrigin = newGraph.Nodes[originalEdge.Origin.Offset];
                var newTarget = newGraph.Nodes[originalEdge.Target.Offset];
                newOrigin.ConnectWith(newTarget, originalEdge.Type);
            }
            
            return newGraph;
        }

        private ControlFlowRegion<AstStatementBase<TInstruction>> TransformRegion(IControlFlowRegion<TInstruction> region)
        {
            switch (region)
            {
                case BasicControlFlowRegion<TInstruction> basicRegion:
                    // Create new basic region.
                    var newBasicRegion = new BasicControlFlowRegion<AstStatementBase<TInstruction>>();
                    TransformSubRegions(basicRegion, newBasicRegion);

                    // Register basic region pair.
                    _regionsMapping[basicRegion] = newBasicRegion;

                    return newBasicRegion;

                case ExceptionHandlerRegion<TInstruction> ehRegion:
                    var newEhRegion = new ExceptionHandlerRegion<AstStatementBase<TInstruction>>();

                    // ProtectedRegion is read-only, so instead we just transform all sub regions and add it to the
                    // existing protected region.
                    TransformSubRegions(ehRegion.ProtectedRegion, newEhRegion.ProtectedRegion);
                    _regionsMapping[ehRegion.ProtectedRegion] = newEhRegion.ProtectedRegion;

                    // Add handler regions.
                    foreach (var subRegion in ehRegion.HandlerRegions)
                        newEhRegion.HandlerRegions.Add(TransformRegion(subRegion));

                    return newEhRegion;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(region));
            }

            void TransformSubRegions(
                BasicControlFlowRegion<TInstruction> originalRegion, 
                BasicControlFlowRegion<AstStatementBase<TInstruction>> newRegion)
            {
                foreach (var subRegion in originalRegion.Regions)
                    newRegion.Regions.Add(TransformRegion(subRegion));
            }
        }

        private BasicBlock<AstStatementBase<TInstruction>> TransformBlock(BasicBlock<TInstruction> block)
        {
            static IVariable[] CreateVariablesBuffer(int count) =>
                count == 0 ? Array.Empty<IVariable>() : new IVariable[count];
            
            int phiCount = 0;
            var result = new BasicBlock<AstStatementBase<TInstruction>>(block.Offset);

            foreach (var instruction in block.Instructions)
            {
                long offset = Architecture.GetOffset(instruction);
                var dataFlowNode = _dataFlowGraph.Nodes[offset];
                var stackDependencies = dataFlowNode.StackDependencies;
                var variableDependencies = dataFlowNode.VariableDependencies;
                var targetVariables = CreateVariablesBuffer(
                    stackDependencies.Count + variableDependencies.Count);

                for (int i = 0; i < stackDependencies.Count; i++)
                {
                    var sources = stackDependencies[i];
                    if (sources.Count == 1)
                    {
                        var source = sources.First();
                        if (source.Node.IsExternal)
                        {
                            targetVariables[i] = new AstVariable(((ExternalDataSourceNode<TInstruction>) source.Node).Name);
                        }
                        else
                        {
                            var slot = _stackSlots[source.Node.Id][source.SlotIndex];
                            targetVariables[i] = slot;
                        }
                    }
                    else
                    {
                        var phiVar = new AstVariable($"phi_{_phiVarCount++}");
                        
                        var slots = sources
                            .Select(s => _stackSlots[s.Node.Id][s.SlotIndex]);
                        var variables = slots
                            .Select(s => new AstVariableExpression<TInstruction>(_id--, s));
                        
                        var phiStatement = new AstPhiStatement<TInstruction>(
                            _id--, variables.ToArray(), phiVar);
                        
                        result.Instructions.Insert(phiCount++, phiStatement);
                        targetVariables[i] = phiVar;
                    }
                }

                int index = stackDependencies.Count;
                foreach (var variable in variableDependencies)
                    targetVariables[index++] = variable.Key;
                
                var instructionExpression = new AstInstructionExpression<TInstruction>(
                    offset,
                    instruction,
                    targetVariables
                        .Select(t => new AstVariableExpression<TInstruction>(_id--, t))
                        .ToArray()
                );

                var writtenVariables = CreateVariablesBuffer(
                    Architecture.GetWrittenVariablesCount(instruction));
                if (writtenVariables.Length > 0)
                    Architecture.GetWrittenVariables(instruction, writtenVariables.AsSpan());

                if (!dataFlowNode.GetDependants().Any() && writtenVariables.Length == 0)
                {
                    result.Instructions.Add(new AstExpressionStatement<TInstruction>(_id--, instructionExpression));
                }
                else
                {
                    int stackPushCount = Architecture.GetStackPushCount(instruction);
                    var slots = stackPushCount == 0
                        ? Array.Empty<AstVariable>()
                        : CreateVariablesBuffer(stackPushCount)
                            .Select(v => new AstVariable($"stack_slot{_varCount++}"))
                            .ToArray();

                    var combined = CreateVariablesBuffer(writtenVariables.Length + slots.Length);
                    slots.CopyTo(combined, 0);
                    writtenVariables.CopyTo(combined, Math.Max(0, slots.Length - 1));

                    _stackSlots[offset] = slots;
                    result.Instructions.Add(
                        new AstAssignmentStatement<TInstruction>(_id--, instructionExpression, combined));
                }
            }

            return result;
        }

    }
}