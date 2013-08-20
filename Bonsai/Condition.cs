﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bonsai
{
    [WorkflowElementCategory(ElementCategory.Condition)]
    public abstract class Condition : Combinator
    {
    }

    [WorkflowElementCategory(ElementCategory.Condition)]
    public abstract class Condition<TSource>
    {
        public abstract IObservable<TSource> Process(IObservable<TSource> source);
    }
}
