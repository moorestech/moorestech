import block from './block.json';
import item from './item.json';
import blockRefTest from './blockRefTest.json';
import { RefResolver } from 'json-schema-ref-resolver'
import Ajv from 'ajv';

const refResolver = new RefResolver()
refResolver.addSchema(block)
refResolver.addSchema(blockRefTest)
refResolver.addSchema(item)

const ajv = new Ajv({ allErrors: true })
ajv.addSchema(refResolver.getDerefSchema('blocks'), '/block')
ajv.addSchema(refResolver.getDerefSchema('items'), '/item')

export default {
  validator: ajv,
  schemas: {
    block: {
      name: 'Block',
      schema: block
    },
    item: {
      name: 'Item',
      schema: item
    }
  }
}
