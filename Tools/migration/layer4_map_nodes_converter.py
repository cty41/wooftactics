from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

BATCH="pure-run-layer4-map-nodes-v1"
EXPECTED={"map.generator","encounter.catalog","node.transaction","node.rest","node.store","event.attribute-check","event.lost-villager","event.fallen-altar","event.cursed-chest"}

def sha(path:Path)->str:return "sha256:"+hashlib.sha256(path.read_bytes()).hexdigest()
def main()->None:
 p=argparse.ArgumentParser();p.add_argument('--export',required=True);p.add_argument('--specification',required=True);p.add_argument('--output',required=True);a=p.parse_args()
 export=json.loads(Path(a.export).read_text(encoding='utf-8'));spec=json.loads(Path(a.specification).read_text(encoding='utf-8'))
 if export['batchId']!=BATCH or spec['batchId']!=BATCH:raise ValueError('batch identity mismatch')
 assets={x['sourceKey']:x for x in export['assets']}
 if set(assets)!=EXPECTED:raise ValueError('source set drift')
 for item in spec['assets']:
  actual=assets[item['sourceKey']]
  if actual['sourcePath']!=item['sourcePath'] or actual['gitBlobSha1']!=item['gitBlobSha1']:raise ValueError('source binding drift')
 events=[]
 for key,cid in [('event.lost-villager','event.pure-run.lost-villager'),('event.fallen-altar','event.pure-run.fallen-altar'),('event.cursed-chest','event.pure-run.cursed-chest')]:
  source=Path(assets[key]['sourcePath']);data=json.loads(source.read_text(encoding='utf-8'))
  options=[]
  for option in data['options']:
   options.append({'id':option['id'],'text':option['text'],'attribute':'Agility' if option['attribute']=='Dexterity' else option['attribute'],'baseSuccessRate':option['baseSuccessRate'],'success':option['success'],'failure':option['failure']})
  events.append({'contentId':cid,'sourceId':data['eventId'],'title':data['title'],'description':data['description'],'options':options,'sourcePath':source.as_posix(),'sourceSha256':sha(source)})
 draft={'schemaVersion':1,'batchId':BATCH,'source':{'tag':export['sourceTag'],'commit':export['sourceCommit'],'unityVersion':export['unityVersion'],'exportHash':sha(Path(a.export))},'map':{'contentId':'run-map.pure-run.layer4-v1','layoutVersion':2,'nodes':['start','layer_01_battle','layer_02_battle','layer_03_battle','layer_04_battle','layer_04_rest','layer_04_store','layer_04_event'],'layer4':['layer_04_battle','layer_04_rest','layer_04_store','layer_04_event']},'encounter':{'contentId':'encounter.pure-run.n4','layout':'battle-layout.pure-run.split-flank','monsters':['unit.pure-run.goat-ranged','unit.pure-run.goat-ranged','unit.pure-run.goat-aoe','unit.pure-run.goat-charger']},'rest':{'contentId':'rest.pure-run.standard-v1','healPercent':30,'manaPercent':30},'store':{'contentId':'store.pure-run.standard-v1','stockSize':3,'consumableGuarantee':1},'events':events,'excluded':['N5','N6','E1','E2','Special','Treasure','Lv3']}
 Path(a.output).write_text(json.dumps(draft,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
if __name__=='__main__':main()
