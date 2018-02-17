﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Sandwych.MapMatchingKit.Markov
{
    public interface IStateCandidate<TCandidate, TTransition, TSample> : IEquatable<TCandidate>
        where TCandidate : IStateCandidate<TCandidate, TTransition, TSample>
    {
        double Seqprob { get; set; }
        double Filtprob { get; set; }
        TCandidate Predecessor { get; set; }
        TTransition Transition { get; set; }
        bool HasTransition { get; }
    }
}
