import block from './blocks.json';
import item from './items.json';
import challenge from './challenges.json';
import craftRecipes from './craftRecipes.json';
import mapObjects from './mapObjects.json';
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

refResolver.addSchema(gearConnects)
refResolver.addSchema(inputConnects)
refResolver.addSchema(modelTransform)

const ajv = new Ajv({ allErrors: true })
ajv.addSchema(refResolver.getDerefSchema('blocks'), '/block')
ajv.addSchema(refResolver.getDerefSchema('items'), '/item')
ajv.addSchema(refResolver.getDerefSchema('craftRecipes'), '/craftRecipes')
ajv.addSchema(refResolver.getDerefSchema('challenges'), '/challenge')
ajv.addSchema(refResolver.getDerefSchema('mapObjects'), '/mapObjects')

export default {
  validator: ajv,
  schemas: {
    block: {
      name: 'Block',
      schema: refResolver.getDerefSchema('blocks')
    },
    item: {
      name: 'Item',
      schema: refResolver.getDerefSchema('items')
    },
    craftRecipes: {
      name: 'CraftRecipe',
      schema: refResolver.getDerefSchema('craftRecipes')
    },
    challenge: {
      name: 'Challenge',
      schema: refResolver.getDerefSchema('challenges')
    },
    mapObject: {
      name: 'MapObjects',
      schema: refResolver.getDerefSchema('mapObjects')
    }
  }
}
