import block from './block.json';
import item from './item.json';
import gearConnects from './gearConnects.json';
import inputConnects from './inputConnects.json';
import modelTransform from './modelTransform.json';
import craftRecipes from './craftRecipes.json';
import { RefResolver } from 'json-schema-ref-resolver'
import Ajv from 'ajv';

const refResolver = new RefResolver()
refResolver.addSchema(block)
refResolver.addSchema(gearConnects)
refResolver.addSchema(inputConnects)
refResolver.addSchema(modelTransform)
refResolver.addSchema(item)
refResolver.addSchema(craftRecipes)

const ajv = new Ajv({ allErrors: true })
ajv.addSchema(refResolver.getDerefSchema('blocks'), '/block')
ajv.addSchema(refResolver.getDerefSchema('items'), '/item')
ajv.addSchema(refResolver.getDerefSchema('craftRecipes'), '/craftRecipes')

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
    }
  }
}
