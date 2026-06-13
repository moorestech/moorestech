import subprocess, re, sys
def sh(*a): return subprocess.run(['git']+list(a),capture_output=True,text=True).stdout
ai=set(sh('log','--since=2026-01-01','-i','--grep','Co-Authored-By: Claude','--format=%H').split())
ai_short={h[:9] for h in ai}
def is_ai(h): return h in ai or h[:9] in ai_short or (sh('log','-1','--format=%B',h).lower().find('co-authored-by: claude')>=0)
# candidate human fix commits (short msgs implying correction) in 3 months
cands = sh('log','--since=2026-03-13','--no-merges','--format=%H|%s').strip().split('\n')
KW=['fix','修正','レビュー','codex','不要','削除','リファクタ','直','revert','戻']
results=[]
for line in cands:
    h,s=line.split('|',1)
    if is_ai(h): continue
    if not any(k in s.lower() for k in KW): continue
    # parse diff, collect removed lines per file with old line numbers
    diff=sh('show',h,'--unified=0','--','*.cs')
    cur=None; oldln=0; blames={}
    for dl in diff.split('\n'):
        if dl.startswith('+++ b/'): cur=dl[6:]
        elif dl.startswith('@@'):
            m=re.search(r'-(\d+)(?:,(\d+))?',dl); oldln=int(m.group(1)) if m else 0
        elif dl.startswith('-') and not dl.startswith('---') and cur:
            if len(dl.strip())>4:  # skip trivial
                blames.setdefault(cur,[]).append(oldln)
            oldln+=1
        elif not dl.startswith('-'):
            if not dl.startswith('+') and not dl.startswith('@@'): oldln+=1 if False else 0
    # blame parent for those lines, find AI authors
    aihits=0; total=0; aicommits=set()
    for f,lns in blames.items():
        for ln in lns[:40]:
            total+=1
            b=sh('blame',f'{h}^','-L',f'{ln},{ln}','--porcelain','--',f)
            bh=b.split(' ',1)[0] if b else ''
            if bh and is_ai(bh): aihits+=1; aicommits.add(bh[:9])
    if aihits>0:
        results.append((h[:9],s[:48],aihits,total,','.join(list(aicommits)[:3])))
results.sort(key=lambda x:-x[2])
print(f"{'human':10}{'aihit/tot':10} ai_commits  subject")
for r in results[:25]:
    print(f"{r[0]:10}{str(r[2])+'/'+str(r[3]):10} {r[4]:32} {r[1]}")
