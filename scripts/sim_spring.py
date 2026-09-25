# -*- coding: utf-8 -*-
# simulate dock magnification spring: old explicit euler vs new semi-implicit with dt clamp
import math, random

STIFF, DAMP = 190.0, 16.0

def simulate(kind, jitter=True, steps=240):
    scale, vel = 1.0, 0.0
    target = 1.78
    trace = []
    random.seed(7)
    for i in range(steps):
        # dt: nominal 16ms, occasional 100ms spike (UI stall) every ~40 frames
        dt = 0.016
        if jitter and i % 40 == 0:
            dt = 0.1
        if kind == 'old':
            vel += (target - scale) * STIFF * dt
            vel -= vel * DAMP * dt
            scale += vel * dt            # explicit: unstable for w*dt>0.5
        else:
            d = min(dt, 1.0/30.0)        # clamp
            vel += (target - scale) * STIFF * d
            vel *= math.exp(-DAMP * d)   # exact exponential damping
            scale += vel * d             # semi-implicit: stable
        scale = min(max(scale, 0.9), 1.88)
        trace.append(scale)
    return trace

for kind in ['old', 'new']:
    t = simulate(kind)
    tgt = 1.78
    peak = max(t)
    deltas = [t[i+1]-t[i] for i in range(len(t)-1)]
    signs = sum(1 for i in range(len(deltas)-1) if deltas[i]*deltas[i+1] < 0)
    final = t[-1]
    print('%s: peak=%.2f final=%.3f overshoot=%+.1f%% direction-flips=%d' %
          (kind, peak, final, (peak-tgt)/tgt*100, signs))

# coupled system: 9 icons, mouse fixed above icon 4; targets from LIVE centers (old)
# vs RESTING centers (new). Shows feedback-loop wobble.
def coupled(use_rest):
    N, BASE, GAP, AMP, SIG = 9, 40.0, 11.0, 0.78, 56.0
    x = 20.0
    rest = []
    for i in range(N):
        rest.append(x + BASE/2)
        x += BASE + GAP
    mouse = rest[4]
    scales = [1.0]*N
    vels = [0.0]*N
    wobble = 0.0
    for step in range(300):
        # layout from current scales
        total = sum(BASE*s for s in scales) + GAP*(N-1)
        x0 = 20.0
        cx, xx = [], x0
        for i in range(N):
            cx.append(xx + BASE*scales[i]/2)
            xx += BASE*scales[i] + GAP
        for i in range(N):
            ref = rest[i] if use_rest else cx[i]
            tgt = 1.0 + AMP*math.exp(-((mouse-ref)**2)/(2*SIG*SIG))
            d = 0.016
            vels[i] += (tgt - scales[i]) * STIFF * d
            vels[i] *= math.exp(-DAMP*d)
            scales[i] += vels[i]*d
            scales[i] = min(max(scales[i], 0.9), 1.88)
        wobble += abs(cx[4] - rest[4])
    # count direction flips of icon4 center around its convergence point
    hist = []
    scales = [1.0]*N; vels = [0.0]*N
    for step in range(300):
        total = sum(BASE*s for s in scales) + GAP*(N-1)
        xx = 20.0
        cx = []
        for i in range(N):
            cx.append(xx + BASE*scales[i]/2)
            xx += BASE*scales[i] + GAP
        hist.append(cx[4])
        for i in range(N):
            ref = rest[i] if use_rest else cx[i]
            tgt = 1.0 + AMP*math.exp(-((mouse-ref)**2)/(2*SIG*SIG))
            vels[i] += (tgt - scales[i]) * STIFF * 0.016
            vels[i] *= math.exp(-DAMP*0.016)
            scales[i] += vels[i]*0.016
    deltas = [hist[i+1]-hist[i] for i in range(len(hist)-1)]
    flips = sum(1 for i in range(len(deltas)-1) if deltas[i]*deltas[i+1] < 0)
    print('coupled(live centers): icon4 center flips=%d, drift-sum=%.0fpx' % (flips, wobble) if not use_rest
          else 'coupled(rest centers): icon4 center flips=%d, drift-sum=%.0fpx' % (flips, wobble))

coupled(False)   # old: live centers -> feedback
coupled(True)    # new: resting centers -> stable
