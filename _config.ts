import blocks from './blocks.json';
import items from './items.json';
import challenge from './challenges.json';
import craftRecipes from './craftRecipes.json';
import mapObjects from './mapObjects.json';
import machineRecipes from './machineRecipes.json';
import gearConnects from './ref/gearConnects.json';
import inputConnects from './ref/inputConnects.json';
import modelTransform from './ref/modelTransform.json';
import { RefResolver } from 'json-schema-ref-resolver'
import Ajv from 'ajv';

const refResolver = new RefResolver()
refResolver.addSchema(items)
refResolver.addSchema(blocks)
refResolver.addSchema(challenge)
refResolver.addSchema(craftRecipes)
refResolver.addSchema(mapObjects)
refResolver.addSchema(machineRecipes)

refResolver.addSchema(gearConnects)
refResolver.addSchema(inputConnects)
refResolver.addSchema(modelTransform)

const ajv = new Ajv({ allErrors: true })
ajv.addSchema(refResolver.getDerefSchema('blocks'), '/blocks')
ajv.addSchema(refResolver.getDerefSchema('items'), '/items')
ajv.addSchema(refResolver.getDerefSchema('craftRecipes'), '/craftRecipes')
ajv.addSchema(refResolver.getDerefSchema('challenges'), '/challenges')
ajv.addSchema(refResolver.getDerefSchema('mapObjects'), '/mapObjects')
ajv.addSchema(refResolver.getDerefSchema('machineRecipes'), '/machineRecipes')

export default {
  validator: ajv,
  schemas: {
    items: {
      name: 'Items',
      schema: refResolver.getDerefSchema('items')
    },
    blocks: {
      name: 'Blocks',
      schema: refResolver.getDerefSchema('blocks')
    },
    mapObjects: {
      name: 'MapObjects',
      schema: refResolver.getDerefSchema('mapObjects')
    },
    craftRecipes: {
      name: 'CraftRecipes',
      schema: refResolver.getDerefSchema('craftRecipes')
    },
    machineRecipes: {
      name: 'MachineRecipes',
      schema: refResolver.getDerefSchema('machineRecipes')
    },
    challenges: {
      name: 'Challenges',
      schema: refResolver.getDerefSchema('challenges')
    },
  }
}
