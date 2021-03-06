﻿using Sandwych.MapMatchingKit.Markov;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Sandwych.MapMatchingKit.Tests.Markov
{
    public class FilterTest
    {
        public struct MockStateTransition
        {
        }

        private class MockElement : AbstractStateCandidate<MockElement, MockStateTransition, MockSample>, IEquatable<MockElement>
        {
            public int Id { get; }

            public MockElement(MockSample sample, int id) : base(sample)
            {
                this.Id = id;
            }

            public MockElement(int id, double filtprob, double seqprob) : this(default, id)
            {
                this.Filtprob = filtprob;
                this.Seqprob = seqprob;
            }

            public bool Equals(MockElement other)
            {
                return this.Id == other.Id;
            }
        }

        private class MockStates
        {
            private readonly double[][] matrix;
            private readonly double[] seqprob;
            private readonly double[] filtprob;
            private readonly int[] pred;

            public MockStates(double[][] matrix)
            {
                this.matrix = matrix;
                this.seqprob = new double[NumCandidates];
                this.filtprob = new double[NumCandidates];
                this.pred = new int[NumCandidates];

                Array.Fill(seqprob, Double.NegativeInfinity);
                Array.Fill(filtprob, 0);

                calculate();
            }

            private void calculate()
            {
                double normsum = 0;
                for (int c = 0; c < NumCandidates; ++c)
                {
                    bool transition = false;
                    for (int p = 0; p < NumPredecessors; ++p)
                    {
                        var pred = Predecessor(p);
                        if (Transition(p, c) == 0)
                        {
                            continue;
                        }
                        transition = true;
                        this.filtprob[c] += pred.Item1 * Transition(p, c);
                        double seqprob =
                            pred.Item2 + Math.Log10(Transition(p, c)) + Math.Log10(Emission(c));

                        if (seqprob > this.seqprob[c])
                        {
                            this.pred[c] = p;
                            this.seqprob[c] = seqprob;
                        }
                    }

                    if (transition == false)
                    {
                        this.filtprob[c] = Emission(c);
                        this.seqprob[c] = Math.Log10(Emission(c));
                        this.pred[c] = -1;
                    }
                    else
                    {
                        this.filtprob[c] *= Emission(c);
                    }

                    normsum += this.filtprob[c];
                }
                for (int c = 0; c < NumCandidates; ++c)
                {
                    this.filtprob[c] /= normsum;
                }
            }

            public int NumCandidates => matrix[0].Length - 2;

            public int NumPredecessors => matrix.Length - 1;

            public double Emission(int candidate) => matrix[0][candidate + 2];

            public double Transition(int predecessor, int candidate) => matrix[predecessor + 1][candidate + 2];

            public (double, double) Predecessor(int predecessor) =>
                (matrix[predecessor + 1][0], Math.Log10(matrix[predecessor + 1][1]));

            public double Seqprob(int candidate)
            {
                return seqprob[candidate];
            }

            public double Filtprob(int candidate)
            {
                return filtprob[candidate];
            }

            public int Pred(int candidate)
            {
                return pred[candidate];
            }
        };

        private class MockFilter : AbstractFilter<MockElement, MockStateTransition, MockSample>
        {
            private readonly MockStates states;

            public MockFilter(MockStates states)
            {
                this.states = states;
            }

            public override IEnumerable<CandidateProbability> ComputeCandidates(
                IEnumerable<MockElement> predecessors, in MockSample sample)
            {
                var results = new CandidateProbability[states.NumCandidates];
                for (int c = 0; c < states.NumCandidates; ++c)
                {
                    results[c] = new CandidateProbability(new MockElement(sample, c), states.Emission(c));
                }
                return results;
            }


            public override TransitionProbability ComputeTransition(
                    in SampleCandidate predecessor,
                    in SampleCandidate candidate)
            {
                return new TransitionProbability(
                    new MockStateTransition(), states.Transition(predecessor.Candidate.Id, candidate.Candidate.Id));
            }

            public ICollection<MockElement> Execute()
            {
                var predecessors = new List<MockElement>();
                for (int p = 0; p < states.NumPredecessors; ++p)
                {
                    var pred = states.Predecessor(p);
                    predecessors.Add(new MockElement(p, pred.Item1, pred.Item2));
                }
                return Execute(predecessors, new MockSample(0), new MockSample(1));
            }
        }

        [Fact]
        public void FilterTestInitial()
        {
            var states = new MockStates(new double[][] {
                new double[] { 0, 0, 0.6, 1.0, 0.4 }
            });
            var filter = new MockFilter(states);

            var result = filter.Execute();

            Assert.Equal(states.NumCandidates, result.Count);

            foreach (var element in result)
            {
                Assert.Equal(states.Filtprob(element.Id), element.Filtprob, (int)10E-6);
                Assert.Equal(states.Seqprob(element.Id), element.Seqprob, (int)10E-6);
                if (states.Pred(element.Id) == -1)
                {
                    Assert.Null(element.Predecessor);
                    Assert.False(element.HasTransition);
                }
                else
                {
                    Assert.Equal(states.Pred(element.Id), element.Predecessor.Id);
                    Assert.False(element.HasTransition);
                }
            }
        }

        [Fact]
        public void FilterTestSubsequent()
        {
            var states = new MockStates(new double[][] {
                new double[] {0, 0, 0.6, 1.0, 0.4},
                new double[] {0.2, 0.3, 0.01, 0.02, 0.3},
                new double[] {0.3, 0.4, 0.2, 0.05, 0.02}
            });
            var filter = new MockFilter(states);

            var result = filter.Execute();

            Assert.Equal(states.NumCandidates, result.Count);

            foreach (var element in result)
            {
                Assert.Equal(states.Filtprob(element.Id), element.Filtprob, (int)10E-6);
                Assert.Equal(states.Seqprob(element.Id), element.Seqprob, (int)10E-6);
                if (states.Pred(element.Id) == -1)
                {
                    Assert.Null(element.Predecessor);
                    Assert.False(element.HasTransition);
                }
                else
                {
                    Assert.Equal(states.Pred(element.Id), element.Predecessor.Id);
                    Assert.True(element.HasTransition);
                }
            }
        }

        [Fact]
        public void FilterTestBreakTransition()
        {
            var states = new MockStates(new double[][] {
                new double[] { 0, 0, 0.6, 1.0, 0.4 },
                new double[] { 0.2, 0.3, 0, 0, 0 },
                new double[] { 0.3, 0.4, 0, 0, 0 }
            });
            var filter = new MockFilter(states);

            var result = filter.Execute();

            Assert.Equal(states.NumCandidates, result.Count);

            foreach (var element in result)
            {
                Assert.Equal(states.Filtprob(element.Id), element.Filtprob, (int)10E-6);
                Assert.Equal(states.Seqprob(element.Id), element.Seqprob, (int)10E-6);
                if (states.Pred(element.Id) == -1)
                {
                    Assert.Null(element.Predecessor);
                    Assert.False(element.HasTransition);
                }
                else
                {
                    Assert.Equal(states.Pred(element.Id), element.Predecessor.Id);
                    Assert.True(element.HasTransition);
                }
            }
        }

        [Fact]
        public void FilterTestBreakCandidates()
        {
            var states = new MockStates(new double[][] {
                new double[] { 0, 0 },
                new double[] { 0.2, 0.3 },
                new double[] { 0.3, 0.4 }
            });
            var filter = new MockFilter(states);

            var result = filter.Execute();

            Assert.Equal(states.NumCandidates, result.Count);

            foreach (var element in result)
            {
                Assert.Equal(states.Filtprob(element.Id), element.Filtprob, (int)10E-6);
                Assert.Equal(states.Seqprob(element.Id), element.Seqprob, (int)10E-6);
                if (states.Pred(element.Id) == -1)
                {
                    Assert.Null(element.Predecessor);
                    Assert.False(element.HasTransition);
                }
                else
                {
                    Assert.Equal(states.Pred(element.Id), element.Predecessor.Id);
                    Assert.True(element.HasTransition);
                }
            }
        }

    }
}
