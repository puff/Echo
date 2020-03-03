using System.IO;
using Echo.ControlFlow.Construction.Static;
using Echo.ControlFlow.Editing;
using Echo.ControlFlow.Serialization.Dot;
using Echo.Core.Graphing.Serialization.Dot;
using Echo.Platforms.DummyPlatform.Code;
using Xunit;

namespace Echo.ControlFlow.Tests.Editing
{
    public class FlowControlSynchronizerTest
    {
        private readonly StaticFlowGraphBuilder<DummyInstruction> _builder;

        public FlowControlSynchronizerTest()
        {
            _builder = new StaticFlowGraphBuilder<DummyInstruction>(DummyArchitecture.Instance, DummyArchitecture.Instance.SuccessorResolver);
        }
        
        [Fact]
        public void NoChangeShouldResultInNoChangeInTheGraph()
        {
            var cfg = _builder.ConstructFlowGraph(new[]
            { 
                DummyInstruction.Ret(0)
            },0);
            
            var synchronizer = new FlowControlSynchronizer<DummyInstruction>(cfg, _builder.SuccessorResolver);
            Assert.False(synchronizer.UpdateFlowControl());
        }

        [Fact]
        public void BranchTargetChangeToAnotherNodeHeaderShouldUpdateFallThroughEdge()
        {
            var cfg = _builder.ConstructFlowGraph(new[]
            { 
                DummyInstruction.Op(0,0, 0),
                DummyInstruction.Jmp(1, 10),
                
                DummyInstruction.Jmp(10, 20),
                
                DummyInstruction.Ret(20)
            },0);
            
            // Change branch target of the first jmp to the ret at offset 20.
            cfg.Nodes[0].Contents.Footer.Operands[0] = 20L;
            
            var synchronizer = new FlowControlSynchronizer<DummyInstruction>(cfg, _builder.SuccessorResolver);
            Assert.True(synchronizer.UpdateFlowControl());
            Assert.Same(cfg.Nodes[20], cfg.Nodes[0].FallThroughNeighbour);
        }

        [Fact]
        public void ConditionalBranchTargetChangeToAnotherNodeHeaderShouldUpdateConditionalEdge()
        {
            var cfg = _builder.ConstructFlowGraph(new[]
            { 
                DummyInstruction.Push(0,1),
                DummyInstruction.JmpCond(1, 20),
                
                DummyInstruction.Jmp(2, 20),
                
                DummyInstruction.Ret(20)
            },0);
            
            // Add a new node to use as a branch target.
            var newTarget = new ControlFlowNode<DummyInstruction>(100, DummyInstruction.Jmp(100, 20));
            cfg.Nodes.Add(newTarget);
            newTarget.ConnectWith(cfg.Nodes[20]);
            
            // Update branch target.
            cfg.Nodes[0].Contents.Footer.Operands[0] = 100L;
            
            var synchronizer = new FlowControlSynchronizer<DummyInstruction>(cfg, _builder.SuccessorResolver);
            Assert.True(synchronizer.UpdateFlowControl());
            Assert.Single(cfg.Nodes[0].ConditionalEdges);
            Assert.True(cfg.Nodes[0].ConditionalEdges.Contains(cfg.Nodes[100]));
        }

        [Fact]
        public void SwapUnconditionalWithConditionalBranchShouldUpdateFallThroughAndConditionalEdge()
        {
            var cfg = _builder.ConstructFlowGraph(new[]
            { 
                DummyInstruction.Push(0,1),
                DummyInstruction.Jmp(1, 10),
                
                DummyInstruction.Jmp(2, 20),
                
                DummyInstruction.Jmp(10, 2),
                
                DummyInstruction.Ret(20)
            },0);

            // Update unconditional jmp to a conditional one.
            var blockInstructions = cfg.Nodes[0].Contents.Instructions;
            blockInstructions[blockInstructions.Count - 1] = DummyInstruction.JmpCond(1, 20);

            var synchronizer = new FlowControlSynchronizer<DummyInstruction>(cfg, _builder.SuccessorResolver);
            Assert.True(synchronizer.UpdateFlowControl());
            Assert.Same(cfg.Nodes[2], cfg.Nodes[0].FallThroughNeighbour);
            Assert.Single(cfg.Nodes[0].ConditionalEdges);
            Assert.True(cfg.Nodes[0].ConditionalEdges.Contains(cfg.Nodes[20]));
        }

        [Fact]
        public void ChangeBranchTargetToMiddleOfNodeShouldSplitNode()
        {
            var instructions = new[]
            { 
                DummyInstruction.Push(0,1),
                DummyInstruction.Jmp(1, 10),
                
                DummyInstruction.Op(10, 0,0),
                DummyInstruction.Op(11, 0,0),
                DummyInstruction.Op(12, 0,0),
                DummyInstruction.Op(13, 0,0),
                DummyInstruction.Op(14, 0,0),
                DummyInstruction.JmpCond(15, 10),
                
                DummyInstruction.Ret(16)
            };
            var cfg = _builder.ConstructFlowGraph(instructions,0);

            // Change jmp target to an instruction in the middle of node[10].
            cfg.Nodes[0].Contents.Footer.Operands[0] = 13L;
            
            var synchronizer = new FlowControlSynchronizer<DummyInstruction>(cfg, _builder.SuccessorResolver);
            Assert.True(synchronizer.UpdateFlowControl());
            
            Assert.True(cfg.Nodes.Contains(10), "Original target does not exist anymore.");
            Assert.True(cfg.Nodes.Contains(13), "Original target was not split up correctly.");
            
            Assert.Same(cfg.Nodes[13], cfg.Nodes[10].FallThroughNeighbour);
            Assert.Same(cfg.Nodes[13], cfg.Nodes[0].FallThroughNeighbour);
        }
        
    }
}