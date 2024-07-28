import block from './blocks.json';
import item from './items.json';
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
refResolver.addSchema(item)
refResolver.addSchema(block)
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
    item: {
      name: 'Items',
      schema: refResolver.getDerefSchema('items')
    },
    block: {
      name: 'Blocks',
      schema: refResolver.getDerefSchema('blocks')
    },
    mapObject: {
      name: 'MapObjects',
      schema: refResolver.getDerefSchema('mapObjects')
    },
    craftRecipes: {
      name: 'CraftRecipes',
      schema: refResolver.getDerefSchema('craftRecipes')
    },
    machineRecipe: {
      name: 'MachineRecipes',
      schema: refResolver.getDerefSchema('machineRecipes')
    },
    challenge: {
      name: 'Challenges',
      schema: refResolver.getDerefSchema('challenges')
    },
  }
}
