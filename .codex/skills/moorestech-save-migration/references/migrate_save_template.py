"""Step 5: moorestech セーブ移行の雛形。
- /tmp/id_maps.json (dump_id_maps.cs の出力) を読む
- 形式ごとの t_* 関数を「Step 1 で列挙した変更」に合わせて実装する（下記は実例）
- backup -> 変換 -> 安全スキャン -> 書き戻し
使い方: SAVE と stats 対象の形式関数を調整して python3 migrate_save_template.py
"""
import json, os, shutil, datetime, sys, base64, struct

SAVE = os.path.expanduser("~/Library/Application Support/moorestech/saves/save_1.json")
MAPS = json.load(open("/tmp/id_maps.json"))
ITEM, FLUID = MAPS["items"], MAPS["fluids"]
ITEM_CONST = MAPS.get("itemConst", "Item")
EMPTY = "00000000-0000-0000-0000-000000000000"
missing_items, missing_fluids = set(), set()

def iguid(i):
    i=int(i)
    if i==0: return EMPTY
    g=ITEM.get(str(i));  return g if g is not None else (missing_items.add(i) or None)
def fguid(i):
    i=int(i)
    if i==0: return EMPTY
    g=FLUID.get(str(i)); return g if g is not None else (missing_fluids.add(i) or None)

stats={}
def bump(k): stats[k]=stats.get(k,0)+1

# ---- 形式別変換（Step 1 の列挙に合わせて足し引きする。以下は実例） ----
def t_fuelgear_fluid(o):  # flat {FluidId,Amount,...} -> {Fluid:{fluidGuid,amount},...}
    return {"Fluid":{"fluidGuid":fguid(o.get("FluidId",0)),"amount":o.get("Amount",0.0)},
            "WasRefilledThisUpdate":o.get("WasRefilledThisUpdate",False),
            "ConsecutiveUpdatesWithoutRefill":o.get("ConsecutiveUpdatesWithoutRefill",0),
            "WasEverDisconnected":o.get("WasEverDisconnected",False)}
def t_fluid_container(o):  # FluidPipe/Pump {fluidId,amount,capacity} -> {fluidGuid,amount}
    return {"fluidGuid":fguid(o.get("fluidId",0)),"amount":o.get("amount",0.0)}
def t_machine(o):          # inputFluidSlot/outputFluidSlot の {fluidId,amount} -> {fluidGuid,amount}
    for s in ("inputFluidSlot","outputFluidSlot"):
        if o.get(s): o[s]=[{"fluidGuid":fguid(x.get("fluidId",0)),"amount":x.get("amount",0.0)} for x in o[s]]
    return o
def t_gearchain(o):        # connections の itemId -> itemGuid
    for c in o.get("connections",[]):
        if "itemId" in c: c["itemGuid"]=iguid(c.pop("itemId"))
    return o
def t_trainplatform_item(o):  # MessagePack [[pairs]] -> {items:[{itemGuid,count}]}
    return {"items":[{"itemGuid":iguid(p[0]),"count":p[1]} for p in o[0]]}

def _mp(b,i):
    t=b[i]; i+=1
    if t==0xCA: return struct.unpack('>f',b[i:i+4])[0],i+4
    if t==0xCB: return struct.unpack('>d',b[i:i+8])[0],i+8
    if t==0xD2: return struct.unpack('>i',b[i:i+4])[0],i+4
    if t<0x80:  return t,i
    if t>=0xE0: return t-256,i
    raise ValueError("msgpack tag %s"%hex(t))
def t_rail(b64):           # base64 MessagePack [[x,y,z]] -> {"x","y","z"}
    b=base64.b64decode(b64); i=2  # 0x91 array1, 0x93 array3
    x,i=_mp(b,i); y,i=_mp(b,i); z,i=_mp(b,i)
    return json.dumps({"x":x,"y":y,"z":z},separators=(",",":"),ensure_ascii=False)
def t_train_container(v0):  # ["TypeName",[[id,count]...]] -> {"containerType","containerState"}
    arr=json.loads(v0)
    if "ItemTrainCarContainer" not in arr[0]: return None
    items=[{"itemGuid":iguid(p[0]),"count":p[1]} for p in arr[1]]
    state=json.dumps(items,separators=(",",":"),ensure_ascii=False)
    return json.dumps({"containerType":ITEM_CONST,"containerState":state},separators=(",",":"),ensure_ascii=False)

raw=open(SAVE,encoding="utf-8").read(); d=json.loads(raw)
for b in d.get("world",[]):
    st=b.get("state",{})
    for k in list(st.keys()):
        v=st[k]
        if not isinstance(v,str): continue
        if k.endswith("RailComponentStateDetailComponent"): st[k]=t_rail(v); bump("Rail"); continue
        try: o=json.loads(v)
        except Exception: continue
        new=None
        if   k=="fuelGearGeneratorFluid": new=t_fuelgear_fluid(o); bump("FuelGearFluid")
        elif k.endswith("FluidPipeSaveComponent"): new=t_fluid_container(o); bump("FluidPipe")
        elif k.endswith("PumpFluidOutputComponent"): new=t_fluid_container(o); bump("Pump")
        elif k.endswith("VanillaMachineSaveComponent"):
            if o.get("inputFluidSlot") or o.get("outputFluidSlot"): bump("MachineFluid")
            new=t_machine(o)
        elif k=="GearChainPoleComponent": new=t_gearchain(o); bump("GearChain")
        elif k.endswith("TrainPlatformItemContainerComponent"): new=t_trainplatform_item(o); bump("PlatformItem")
        if new is not None: st[k]=json.dumps(new,separators=(",",":"),ensure_ascii=False)

for u in d.get("trainUnits",[]):
    for c in u.get("Cars",[]):
        cs=c.get("ContainerSaveData")
        if isinstance(cs,str) and cs.strip().startswith("["):
            nv=t_train_container(cs)
            if nv is not None: c["ContainerSaveData"]=nv; bump("TrainCar")

# 安全スキャン: 全 state 値が valid JSON でなければ未対応の base64/MessagePack 形式
non_json={}
for b in d.get("world",[]):
    for k,v in b.get("state",{}).items():
        if isinstance(v,str):
            try: json.loads(v)
            except Exception: non_json[k]=non_json.get(k,0)+1
if non_json: print("WARNING unhandled non-JSON states:", non_json, file=sys.stderr)
if missing_items or missing_fluids:
    print("ABORT unresolved ids items=%s fluids=%s"%(sorted(missing_items),sorted(missing_fluids)),file=sys.stderr); sys.exit(1)

print("planned:", stats)
ts=datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
bdir=os.path.join(os.path.dirname(SAVE), f"Backup_{ts}_pre-migration"); os.makedirs(bdir,exist_ok=True)
shutil.copy2(SAVE, os.path.join(bdir,os.path.basename(SAVE))); print("BACKUP ->",bdir)
out=json.dumps(d,separators=(",",":"),ensure_ascii=False)
open(SAVE,"w",encoding="utf-8").write(out); print("WROTE",SAVE,len(raw),"->",len(out))
