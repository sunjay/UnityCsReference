// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

namespace UnityEngine.UIElements
{
    // determines in which event phase an event handler wants to handle events
    // the handler always gets called if it is the target VisualElement
    public enum PropagationPhase
    {
        // Not propagating at the moment.
        None = 0,

        // Propagation from root of tree to immediate parent of target.
        TrickleDown = 1,

        // Event is at target.
        AtTarget = 2,

        // Execute the default action(s) at target.
        DefaultActionAtTarget = 5,

        // After the target has gotten the chance to handle the event, the event walks back up the parent hierarchy back to root.
        BubbleUp = 3,

        // At last, execute the default action(s).
        DefaultAction = 4
    }

    interface IEventDispatchingStrategy
    {
        bool CanDispatchEvent(EventBase evt);
        void DispatchEvent(EventBase evt, IPanel panel);
    }

    static class EventDispatchUtilities
    {
        public static void PropagateEvent(EventBase evt)
        {
            Debug.Assert(!evt.dispatch, "Event is being dispatched recursively.");

            evt.dispatch = true;

            if (evt.path == null)
            {
                (evt.target as CallbackEventHandler)?.HandleEventAtTargetPhase(evt);
            }
            else
            {
                // Phase 1: TrickleDown phase
                // Propagate event from root to target.parent
                if (evt.tricklesDown)
                {
                    evt.propagationPhase = PropagationPhase.TrickleDown;

                    for (int i = evt.path.trickleDownPath.Count - 1; i >= 0; i--)
                    {
                        if (evt.isPropagationStopped)
                            break;

                        if (evt.Skip(evt.path.trickleDownPath[i]))
                        {
                            continue;
                        }

                        evt.currentTarget = evt.path.trickleDownPath[i];
                        evt.currentTarget.HandleEvent(evt);
                    }
                }

                // Phase 2: Target / DefaultActionAtTarget
                // Propagate event from target parent up to root for the target phase

                // Call HandleEvent() even if propagation is stopped, for the default actions at target.
                evt.propagationPhase = PropagationPhase.AtTarget;
                foreach (var element in evt.path.targetElements)
                {
                    if (evt.Skip(element))
                    {
                        continue;
                    }

                    evt.target = element;
                    evt.currentTarget = evt.target;
                    evt.currentTarget.HandleEvent(evt);
                }

                // Call ExecuteDefaultActionAtTarget
                evt.propagationPhase = PropagationPhase.DefaultActionAtTarget;
                foreach (var element in evt.path.targetElements)
                {
                    if (evt.Skip(element))
                    {
                        continue;
                    }

                    evt.target = element;
                    evt.currentTarget = evt.target;
                    evt.currentTarget.HandleEvent(evt);
                }

                // Reset target to original target
                evt.target = evt.leafTarget;

                // Phase 3: bubble up phase
                // Propagate event from target parent up to root
                if (evt.bubbles)
                {
                    evt.propagationPhase = PropagationPhase.BubbleUp;

                    foreach (var element in evt.path.bubbleUpPath)
                    {
                        if (evt.Skip(element))
                        {
                            continue;
                        }

                        evt.currentTarget = element;
                        evt.currentTarget.HandleEvent(evt);
                    }
                }
            }

            evt.dispatch = false;
            evt.propagationPhase = PropagationPhase.None;
            evt.currentTarget = null;
        }

        internal static void PropagateToIMGUIContainer(VisualElement root, EventBase evt)
        {
            if (evt.imguiEvent == null)
            {
                return;
            }

            // Send the event to the first IMGUIContainer that can handle it.

            var imContainer = root as IMGUIContainer;
            if (imContainer != null)
            {
                if (evt.Skip(imContainer))
                {
                    // IMGUIContainer have no children. We can return without iterating the children list.
                    return;
                }

                // Only permit switching the focus to another IMGUIContainer if the event target was not focusable.
                bool targetIsFocusable = (evt.target as Focusable)?.focusable ?? false;
                if (imContainer.SendEventToIMGUI(evt, !targetIsFocusable))
                {
                    evt.StopPropagation();
                    evt.PreventDefault();
                }

                if (evt.imguiEvent.rawType == EventType.Used)
                    Debug.Assert(evt.isPropagationStopped);
            }

            if (root != null)
            {
                for (int i = 0; i < root.hierarchy.childCount; i++)
                {
                    PropagateToIMGUIContainer(root.hierarchy[i], evt);
                    if (evt.isPropagationStopped)
                        break;
                }
            }
        }

        public static void ExecuteDefaultAction(EventBase evt, IPanel panel)
        {
            if (evt.target == null && panel != null)
            {
                evt.target = panel.visualTree;
            }

            if (evt.target != null)
            {
                evt.dispatch = true;
                evt.currentTarget = evt.target;
                evt.propagationPhase = PropagationPhase.DefaultAction;

                evt.currentTarget.HandleEvent(evt);

                evt.propagationPhase = PropagationPhase.None;
                evt.currentTarget = null;
                evt.dispatch = false;
            }
        }
    }
}
